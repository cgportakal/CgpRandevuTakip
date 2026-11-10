using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Infrastructure.Time;

/// <summary>
/// Tüm UTC ↔ yerel dönüşümleri Windows'un sistem saat dilimini kullanır.
/// Kullanıcı işletim sistemi saat dilimini değiştirirse uygulama yeniden başlatıldığında
/// yeni dilim geçerli olur.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.Local);

    public DateTime ToUtc(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
}
