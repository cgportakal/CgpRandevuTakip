namespace CgpRandevuTakip.Api.Notifications;

/// <summary>
/// Sunucu push yapılandırması (<c>Push</c> bölümü veya <c>Push__Topic</c> ortam değişkenleri).
/// Etkin değilse veritabanındaki <c>PushEnabled</c> ayarı kullanılır.
/// </summary>
public sealed class PushOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "ntfy";
    public string Topic { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}
