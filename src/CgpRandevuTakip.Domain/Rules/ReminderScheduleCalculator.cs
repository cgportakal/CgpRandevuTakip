namespace CgpRandevuTakip.Domain.Rules;

public sealed record ReminderPlan(int OffsetMinutes, DateTime DueAtUtc);

/// <summary>
/// Hatırlatıcı zamanlarını hesaplar. Kural: geçmişe düşen hatırlatıcı oluşturulmaz.
/// </summary>
public static class ReminderScheduleCalculator
{
    public static readonly IReadOnlyList<int> AllowedOffsetsMinutes = new[] { 1440, 60, 30 };

    /// <summary>Sıradaki kişi araması için bildirimler: randevudan 30 dk ve 5 dk önce.</summary>
    public static readonly IReadOnlyList<int> CallReminderOffsetsMinutes = new[] { 30, 5 };

    public static IReadOnlyList<ReminderPlan> Calculate(
        DateTime startsAtUtc,
        IEnumerable<int> offsetsMinutes,
        DateTime nowUtc)
    {
        var plans = new List<ReminderPlan>();

        foreach (var offset in offsetsMinutes.Distinct())
        {
            if (!AllowedOffsetsMinutes.Contains(offset))
                throw new ArgumentOutOfRangeException(
                    nameof(offsetsMinutes),
                    offset,
                    "Desteklenmeyen hatırlatıcı aralığı. Yalnızca 1440, 60 ve 30 dakika kullanılabilir.");

            var dueAt = startsAtUtc.AddMinutes(-offset);
            if (dueAt <= nowUtc)
                continue;

            plans.Add(new ReminderPlan(offset, dueAt));
        }

        return plans.OrderBy(p => p.DueAtUtc).ToList();
    }

    public static IReadOnlyList<DateTime> GetCallReminderTimes(DateTime startsAtUtc)
        => CallReminderOffsetsMinutes
            .Select(offset => startsAtUtc.AddMinutes(-offset))
            .OrderBy(due => due)
            .ToList();

    public static string BuildDedupKey(string appointmentId, int offsetMinutes, int generation)
        => $"{appointmentId}:{offsetMinutes}:{generation}";
}
