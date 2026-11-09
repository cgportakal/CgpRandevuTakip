namespace CgpRandevuTakip.Domain.Rules;

/// <summary>
/// Yeni görüşme için varsayılan tarih/saat hesapları.
/// </summary>
public static class AppointmentDefaults
{
    /// <summary>Görüşme saatlerinin alt sınırı (06:00).</summary>
    public static readonly TimeSpan DayStart = TimeSpan.FromHours(6);

    /// <summary>Görüşme saatlerinin üst sınırı (20:45).</summary>
    public static readonly TimeSpan DayEnd = TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(45));

    /// <summary>
    /// Bugün için "şimdi + 1 saat" hedefini 15 dakikalık dilime yukarı yuvarlar.
    /// Hedef gece yarısını aşarsa veya 20:45'ten sonraysa ertesi gün 06:00 döner.
    /// </summary>
    public static DateTime NextStart(DateTime now)
    {
        var target = now.AddHours(1);
        var minutes = (int)Math.Ceiling(target.TimeOfDay.TotalMinutes / 15.0) * 15;

        if (target.Date > now.Date || minutes > DayEnd.TotalMinutes)
            return now.Date.AddDays(1).Add(DayStart);

        if (minutes < DayStart.TotalMinutes)
            minutes = (int)DayStart.TotalMinutes;

        return now.Date.AddMinutes(minutes);
    }
}
