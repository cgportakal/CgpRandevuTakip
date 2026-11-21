using System.Net.Http;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Desktop.Services.Sync;

namespace CgpRandevuTakip.Desktop.Tests;

public class RealtimeClientTests
{
    [Fact]
    public async Task StartAsync_WithoutSession_DoesNotConnect()
    {
        var settings = new Settings();
        var connection = new ApiConnectionService(
            new CgpApiClient(new HttpClient()),
            new ApiSessionStore(settings, new Protector()));

        await using var realtime = new DesktopRealtimeClient(connection);

        await realtime.StartAsync();

        Assert.False(realtime.IsConnected);
        await realtime.StopAsync();
    }

    private sealed class Settings : ISettingsRepository
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync()
            => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(_values, StringComparer.Ordinal));

        public Task SetManyAsync(IReadOnlyDictionary<string, string> values)
        {
            foreach (var (key, value) in values)
                _values[key] = value;

            return Task.CompletedTask;
        }
    }

    private sealed class Protector : ISecretProtector
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
