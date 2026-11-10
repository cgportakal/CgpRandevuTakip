namespace CgpRandevuTakip.Domain.Rules;

/// <summary>
/// Yeni görüşme için varsayılan tarih/saat hesapları ve çalışma saati kuralları.
/// </summary>
public static class AppointmentDefaults
{
    /// <summary>Varsayılan görüşme saatlerinin alt sınırı (06:00).</summary>
    public static readonly TimeSpan DefaultDayStart = TimeSpan.FromHours(6);

    /// <summary>Varsayılan görüşme saatlerinin üst sınırı (20:45).</summary>
    public static readonly TimeSpan DefaultDayEnd = TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(45));

    /// <summary>
    /// Bugün için "şimdi + 1 saat" hedefini 15 dakikalık dilime yukarı yuvarlar.
    /// Hedef gece yarısını aşarsa veya çalışma saati bitişinden sonraysa ertesi gün
    /// başlangıç saatine alınır. <paramref name="allowOutOfHours"/> true ise gün sınırı uygulanmaz.
    /// </summary>
    public static DateTime NextStart(
        DateTime now,
        TimeSpan? dayStart = null,
        TimeSpan? dayEnd = null,
        bool allowOutOfHours = false)
    {
        var start = dayStart ?? DefaultDayStart;
        var end = dayEnd ?? DefaultDayEnd;

        var target = now.AddHours(1);
        var minutes = (int)Math.Ceiling(target.TimeOfDay.TotalMinutes / 15.0) * 15;

        if (allowOutOfHours)
        {
            if (target.Date > now.Date)
                return now.Date.AddDays(1).Add(start);

            return now.Date.AddMinutes(Math.Min(minutes, (24 * 60) - 15));
        }

        if (target.Date > now.Date || minutes > end.TotalMinutes)
            return now.Date.AddDays(1).Add(start);

        if (minutes < start.TotalMinutes)
            minutes = (int)start.TotalMinutes;

        return now.Date.AddMinutes(minutes);
    }

    /// <summary>Çalışma saatleri içinde 15 dakikalık seçenekler üretir.</summary>
    public static IReadOnlyList<string> BuildTimeOptions(
        TimeSpan dayStart,
        TimeSpan dayEnd,
        bool allowOutOfHours)
    {
        var startMinutes = allowOutOfHours ? 0 : (int)dayStart.TotalMinutes;
        var endMinutes = allowOutOfHours ? (24 * 60) - 15 : (int)dayEnd.TotalMinutes;

        if (endMinutes < startMinutes)
            (startMinutes, endMinutes) = (0, (24 * 60) - 15);

        var options = new List<string>();
        for (var minute = startMinutes; minute <= endMinutes; minute += 15)
            options.Add(TimeSpan.FromMinutes(minute).ToString(@"hh\:mm"));

        return options;
    }
}
