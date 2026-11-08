namespace CgpRandevuTakip.Application.Reminders;

/// <summary>Sessiz saat kuralları. Aralıklar gece yarısını aşabilir (ör. 22:00–07:00).</summary>
public static class NotificationPolicy
{
    public static bool IsQuietNow(
        DateTime localNow,
        TimeSpan start,
        TimeSpan end,
        bool enabled)
    {
        if (!enabled || start == end)
            return false;

        var current = localNow.TimeOfDay;

        if (start < end)
            return current >= start && current < end;

        // Gece yarısını aşan aralık: 22:00 → 07:00
        return current >= start || current < end;
    }
}
