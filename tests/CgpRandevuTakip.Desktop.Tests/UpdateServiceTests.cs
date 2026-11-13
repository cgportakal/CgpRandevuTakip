using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Desktop.Services;

namespace CgpRandevuTakip.Desktop.Tests;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly string _root;
    private readonly UpdateRollbackService _rollback;
    private readonly HttpClient _client = new();
    private readonly TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task? _serverLoop;

    public UpdateServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "cgp-update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _rollback = new UpdateRollbackService(_root);

        var port = GetFreePort();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        BaseUrl = $"http://127.0.0.1:{port}";
        _serverLoop = Task.Run(() => ServeAsync(_listener, _cts.Token));
    }

    private string BaseUrl { get; }

    public void Dispose()
    {
        _cts.Cancel();
        _listener?.Stop();
        _client.Dispose();

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test temizliği başarısız olsa da sonucu etkilemez.
        }
    }

    [Fact]
    public async Task CheckAsync_ParsesVersionDigestsAndReleaseNotes()
    {
        var service = CreateService("9.9.9", new string('a', 64), new string('b', 64));

        var result = await service.CheckAsync();

        Assert.True(result.UpdateAvailable);
        Assert.Equal("9.9.9", result.LatestVersion);
        Assert.Equal(new string('a', 64), result.SetupSha256);
        Assert.Equal(new string('b', 64), result.PortableSha256);
        Assert.Contains("Yenilikler", result.ReleaseNotes);
    }

    [Fact]
    public async Task InstallAsync_RejectsDigestMismatch()
    {
        var service = CreateService("9.9.9", new string('0', 64), null);
        var update = BuildSetupUpdate(new string('0', 64));

        var result = await service.InstallAsync(update);

        Assert.False(result.Success);
        Assert.Contains("doğrulanamadı", result.Error);
    }

    [Fact]
    public async Task InstallAsync_RejectsMissingDigest()
    {
        var service = CreateService("9.9.9", null, null);
        var update = BuildSetupUpdate(null);

        var result = await service.InstallAsync(update);

        Assert.False(result.Success);
        Assert.Contains("SHA-256", result.Error);
    }

    private UpdateCheckResult BuildSetupUpdate(string? digest)
        => new(
            UpdateAvailable: true,
            CurrentVersion: "0.0.1",
            LatestVersion: "9.9.9",
            SetupUrl: $"{BaseUrl}/asset",
            PortableUrl: null,
            ReleaseUrl: null,
            SetupSha256: digest);

    private UpdateService CreateService(string tag, string? setupDigest, string? portableDigest)
        => new(_rollback, _client, $"{BaseUrl}/releases/latest?tag={tag}&setup={setupDigest}&portable={portableDigest}");

    private async Task ServeAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token);
            }
            catch
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                using (client)
                await using (var stream = client.GetStream())
                {
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var requestLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(requestLine))
                        return;

                    string? header;
                    do
                    {
                        header = await reader.ReadLineAsync();
                    } while (!string.IsNullOrEmpty(header));

                    var path = requestLine.Split(' ')[1];
                    if (path.StartsWith("/releases/latest", StringComparison.Ordinal))
                    {
                        var query = ParseQuery(path);
                        var setupDigest = query.GetValueOrDefault("setup");
                        var portableDigest = query.GetValueOrDefault("portable");
                        var tag = query.GetValueOrDefault("tag") ?? "9.9.9";

                        var setupDigestJson = setupDigest is null ? "null" : $"\"sha256:{setupDigest}\"";
                        var portableDigestJson = portableDigest is null ? "null" : $"\"sha256:{portableDigest}\"";

                        var json = $$"""
                            {
                              "tag_name": "{{tag}}",
                              "html_url": "{{BaseUrl}}/release",
                              "body": "Yenilikler: test sürümü",
                              "assets": [
                                {
                                  "name": "CGP-RandevuTakip-{{tag}}-Setup.exe",
                                  "browser_download_url": "{{BaseUrl}}/asset",
                                  "digest": {{setupDigestJson}}
                                },
                                {
                                  "name": "CGP-RandevuTakip-{{tag}}-portable.zip",
                                  "browser_download_url": "{{BaseUrl}}/asset",
                                  "digest": {{portableDigestJson}}
                                }
                              ]
                            }
                            """;

                        await WriteResponseAsync(stream, Encoding.UTF8.GetBytes(json), "application/json");
                    }
                    else
                    {
                        await WriteResponseAsync(stream, Encoding.UTF8.GetBytes("fake-update-package"), "application/octet-stream");
                    }
                }
            }, token);
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, byte[] body, string contentType)
    {
        var header = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    private static Dictionary<string, string?> ParseQuery(string path)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var index = path.IndexOf('?');
        if (index < 0)
            return result;

        foreach (var pair in path[(index + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result[Uri.UnescapeDataString(parts[0])] = parts.Length > 1
                ? Uri.UnescapeDataString(parts[1])
                : null;
        }

        return result;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
