using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Reminders;

public sealed record ReminderContextDto(
    string ReminderId,
    string AppointmentId,
    ReminderKind Kind,
    int OffsetMinutes,
    DateTime StartsAtLocal,
    DateTime DueAtLocal,
    string PersonFullName,
    string HostName,
    string CategoryName,
    string PhoneE164,
    AppointmentMethod Method,
    AppointmentStatus Status,
    bool WindowsNotified = false,
    bool PushNotified = false,
    bool IsOverdue = false)
{
    public string TimeLabel => StartsAtLocal.ToString("HH:mm");

    public string DateLabel => StartsAtLocal.ToString("dd/MM/yyyy");

    public string MethodText => Method switch
    {
        AppointmentMethod.Phone => "Telefon",
        AppointmentMethod.FaceToFace => "Yüz yüze",
        AppointmentMethod.WhatsApp => "WhatsApp",
        AppointmentMethod.Online => "İnternet",
        _ => "—"
    };

    public string StatusText => Status switch
    {
        AppointmentStatus.Scheduled => "Planlandı",
        AppointmentStatus.Arrived => "Geldi",
        AppointmentStatus.NoShow => "Gelmedi",
        AppointmentStatus.Postponed => "Ertelendi",
        AppointmentStatus.Cancelled => "İptal",
        AppointmentStatus.Completed => "Tamamlandı",
        _ => "—"
    };

    /// <summary>Bildirimde kişisel veri içermeyen kısa gövde metni.</summary>
    public string AnonymousBody => $"{TimeLabel} · Görüşme";

    /// <summary>Ürün sahibi onayıyla kişi adını içeren bildirim gövdesi.</summary>
    public string NotificationBody => $"{PersonFullName} · {TimeLabel} · {CategoryName}";
}

public sealed record ReminderResult(bool Success, string? Error = null)
{
    public static ReminderResult Ok() => new(true);
    public static ReminderResult Fail(string error) => new(false, error);
}
