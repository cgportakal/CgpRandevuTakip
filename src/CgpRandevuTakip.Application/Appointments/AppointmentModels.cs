using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Appointments;

public sealed record AppointmentInput(
    string FirstName,
    string LastName,
    string Phone,
    DateTime StartsAtLocal,
    int DurationMinutes,
    string HostUserId,
    string CategoryId,
    AppointmentMethod Method,
    string? Description,
    string? StudentFirstName,
    string? StudentLastName,
    IReadOnlyList<int> ReminderOffsetsMinutes,
    bool IgnoreConflicts = false);

public sealed record AppointmentConflictDto(
    string AppointmentId,
    string PersonName,
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    string? CategoryName,
    bool IsSamePerson);

public sealed record AppointmentSaveResult(
    bool Success,
    string? Error,
    string? AppointmentId,
    IReadOnlyList<AppointmentConflictDto>? Conflicts)
{
    public static AppointmentSaveResult Ok(string appointmentId)
        => new(true, null, appointmentId, null);

    public static AppointmentSaveResult Fail(string error)
        => new(false, error, null, null);

    public static AppointmentSaveResult Conflict(
        string error,
        IReadOnlyList<AppointmentConflictDto> conflicts)
        => new(false, error, null, conflicts);
}

public sealed record AppointmentListItemDto(
    string Id,
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    int DurationMinutes,
    string PersonFirstName,
    string PersonLastName,
    string PhoneE164,
    string HostUserId,
    string HostName,
    string CategoryId,
    string CategoryName,
    string CategoryColorHex,
    AppointmentMethod Method,
    AppointmentStatus Status,
    bool IsOverdue,
    bool IsDeleted,
    string? StudentFullName)
{
    public string PersonFullName => $"{PersonFirstName} {PersonLastName}".Trim();

    public string TimeLabel => StartsAtLocal.ToString("HH:mm");

    public string EndTimeLabel => EndsAtLocal.ToString("HH:mm");

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
}

public sealed record AppointmentDetailDto(
    string Id,
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    int DurationMinutes,
    string PersonFirstName,
    string PersonLastName,
    string PhoneE164,
    string HostUserId,
    string HostName,
    string CategoryId,
    string CategoryName,
    string CategoryColorHex,
    AppointmentMethod Method,
    AppointmentStatus Status,
    string? Description,
    string? StudentFirstName,
    string? StudentLastName,
    IReadOnlyList<int> ReminderOffsetsMinutes,
    bool IsOverdue,
    bool IsClosed,
    bool IsDeleted,
    DateTime CreatedAtLocal,
    DateTime UpdatedAtLocal,
    AppointmentOutcomeDto? Outcome,
    string? ClosedByName,
    DateTime? ClosedAtLocal)
{
    public bool HasOutcome => Outcome is not null;

    public string ClosedAtText => ClosedAtLocal is null
        ? "—"
        : ClosedAtLocal.Value.ToString("dd/MM/yyyy HH:mm");

    public string ClosedByNameText => string.IsNullOrWhiteSpace(ClosedByName)
        ? "—"
        : ClosedByName;

    public string PersonFullName => $"{PersonFirstName} {PersonLastName}".Trim();

    public string StudentFullName => $"{StudentFirstName} {StudentLastName}".Trim();

    public string DateLabel => StartsAtLocal.ToString("dd/MM/yyyy");

    public string TimeRangeLabel => $"{StartsAtLocal:HH:mm} – {EndsAtLocal:HH:mm}";

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
}
