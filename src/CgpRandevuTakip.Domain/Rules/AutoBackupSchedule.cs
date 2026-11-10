namespace CgpRandevuTakip.Domain.Rules;

/// <summary>
/// Otomatik yedekleme zamanlaması: günde en fazla 2 yedek.
/// Birinci yedek öğlen, ikinci yedek öğleden sonra yapılan günlük paylaşım
/// sonrası hemen; paylaşım yoksa akşam saatinde alınır.
/// </summary>
public static class AutoBackupSchedule
{
    public static readonly TimeSpan Noon = new(12, 30, 0);

    public static readonly TimeSpan Evening = new(18, 0, 0);

    public static bool ShouldRun(DateTime localNow, int completedToday, bool sharedAfterNoon)
    {
        if (completedToday >= 2)
            return false;

        if (completedToday == 0)
            return localNow.TimeOfDay >= Noon;

        return (sharedAfterNoon && localNow.TimeOfDay >= Noon)
               || localNow.TimeOfDay >= Evening;
    }
}
