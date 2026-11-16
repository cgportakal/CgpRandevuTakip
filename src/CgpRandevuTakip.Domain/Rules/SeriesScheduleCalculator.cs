using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Rules;

/// <summary>
/// Tekrarlayan görüşme serisinin tarihlerini üretir. Tüm hesaplar ilk randevunun
/// yerel saatine göre yapılır; aylık seride ay sonu kırpması uygulanır
/// (31 Ocak → 28 Şubat → 31 Mart).
/// </summary>
public static class SeriesScheduleCalculator
{
    public const int MinCount = 2;
    public const int MaxCount = 12;

    /// <summary>İlk randevu dahil olmak üzere serinin tüm başlangıç zamanları.</summary>
    public static IReadOnlyList<DateTime> Build(
        DateTime firstStartLocal,
        SeriesFrequency frequency,
        int count)
    {
        if (count is < MinCount or > MaxCount)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Seri 2–12 randevu arasında olmalıdır.");

        if (!Enum.IsDefined(frequency))
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Geçersiz seri sıklığı.");

        var dates = new List<DateTime>(count);
        for (var index = 0; index < count; index++)
        {
            dates.Add(Slot(firstStartLocal, frequency, index + 1));
        }

        return dates;
    }

    /// <summary>
    /// Serinin <paramref name="index"/>. tarihi (1 tabanlı). Sayaç sınırı yoktur;
    /// seriye sonradan kayıt eklendiğinde de kullanılır.
    /// </summary>
    public static DateTime Slot(DateTime firstStartLocal, SeriesFrequency frequency, int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Seri sırası 1'den başlar.");

        if (!Enum.IsDefined(frequency))
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Geçersiz seri sıklığı.");

        return frequency switch
        {
            SeriesFrequency.Weekly => firstStartLocal.AddDays(7 * (index - 1)),
            SeriesFrequency.Biweekly => firstStartLocal.AddDays(14 * (index - 1)),
            SeriesFrequency.Monthly => AddMonthsClamped(firstStartLocal, index - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
        };
    }

    /// <summary>Gün numarasını koruyarak ay ekler; hedef ayda o gün yoksa ayın sonuna kırpar.</summary>
    public static DateTime AddMonthsClamped(DateTime anchor, int months)
    {
        var shifted = new DateTime(anchor.Year, anchor.Month, 1, anchor.Hour, anchor.Minute, anchor.Second, anchor.Kind)
            .AddMonths(months);
        var day = Math.Min(anchor.Day, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        return new DateTime(shifted.Year, shifted.Month, day, anchor.Hour, anchor.Minute, anchor.Second, anchor.Kind);
    }
}
