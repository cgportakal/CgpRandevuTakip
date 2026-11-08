using CgpRandevuTakip.Application.Abstractions;

namespace CgpRandevuTakip.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo IstanbulTimeZone = ResolveIstanbulTimeZone();

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime ToLocal(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IstanbulTimeZone);

    public DateTime ToUtc(DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), IstanbulTimeZone);

    private static TimeZoneInfo ResolveIstanbulTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Europe/Istanbul",
                TimeSpan.FromHours(3),
                "Türkiye Saati",
                "Türkiye Saati");
        }
    }
}
