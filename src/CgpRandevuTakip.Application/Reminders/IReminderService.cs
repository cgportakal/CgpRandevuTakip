using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Reminders;

public interface IReminderService
{
    Task<IReadOnlyList<ReminderContextDto>> GetDueAsync(DateTime nowUtc, int batchSize);

    /// <summary>Tekrar gecikmesi dolan gönderilmiş hatırlatıcıları yeniden planlar; sınırı aşanları yanıtsız yapar.</summary>
    Task<int> RequeueExpiredAsync(DateTime nowUtc, TimeSpan repeatDelay, int maxRepeats);

    Task MarkWindowsNotifiedAsync(string reminderId, DateTime nowUtc);

    Task MarkPushNotifiedAsync(string reminderId, DateTime nowUtc);

    Task MarkSentAsync(string reminderId);

    Task MarkMissedAsync(string reminderId, DateTime nowUtc);

    Task<OperationResult> ApplyActionAsync(string reminderId, ReminderAction action, DateTime nowUtc);
}
