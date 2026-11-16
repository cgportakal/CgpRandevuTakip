using System.Text.Json;
using CgpRandevuTakip.Api.Infrastructure;
using WebPush;

namespace CgpRandevuTakip.Api.Notifications;

/// <summary>
/// Web Push VAPID anahtarları. Yapılandırmada yoksa veri klasöründe üretilip saklanır.
/// </summary>
public sealed class VapidKeyProvider
{
    private sealed record StoredKeys(string Subject, string PublicKey, string PrivateKey);

    public VapidKeyProvider(IConfiguration configuration, ApiEnvironment environment)
    {
        var subject = configuration["WebPush:Subject"];
        var publicKey = configuration["WebPush:PublicKey"];
        var privateKey = configuration["WebPush:PrivateKey"];

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            var stored = LoadOrCreate(environment, subject);
            subject = stored.Subject;
            publicKey = stored.PublicKey;
            privateKey = stored.PrivateKey;
        }

        Subject = string.IsNullOrWhiteSpace(subject) ? "mailto:admin@cgp.local" : subject;
        PublicKey = publicKey!;
        PrivateKey = privateKey!;
        Details = new VapidDetails(Subject, PublicKey, PrivateKey);
    }

    public string Subject { get; }
    public string PublicKey { get; }
    public string PrivateKey { get; }
    public VapidDetails Details { get; }

    private static StoredKeys LoadOrCreate(ApiEnvironment environment, string? subject)
    {
        var path = Path.Combine(environment.RootDirectory, "vapid.json");

        if (File.Exists(path))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<StoredKeys>(File.ReadAllText(path));
                if (stored is not null
                    && !string.IsNullOrWhiteSpace(stored.PublicKey)
                    && !string.IsNullOrWhiteSpace(stored.PrivateKey))
                {
                    return stored;
                }
            }
            catch
            {
                // Bozuk dosya yeniden üretilir.
            }
        }

        var generated = VapidHelper.GenerateVapidKeys();
        var keys = new StoredKeys(
            string.IsNullOrWhiteSpace(subject) ? "mailto:admin@cgp.local" : subject,
            generated.PublicKey,
            generated.PrivateKey);

        File.WriteAllText(path, JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }));
        return keys;
    }
}
