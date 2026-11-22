using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Appointments;

public enum ConflictKind
{
    Host = 0,
    Room = 1,
    Guest = 2
}

/// <summary>
/// Randevuya eklenecek misafir. PersonId verilirse mevcut kişi kullanılır; aksi hâlde
/// telefon + ad ile eşleşen kişi bulunur, yoksa yeni kişi oluşturulur.
/// </summary>
public sealed record AppointmentGuestInput(
    string? PersonId,
    string FirstName,
    string LastName,
    string Phone,
    string? Email = null,
    string? Organization = null,
    bool IsPrimary = false);

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
    bool IgnoreConflicts = false,
    string? ClientRequestId = null,
    DateTime? ExpectedUpdatedAtUtc = null,
    string? RoomId = null,
    IReadOnlyList<string>? ParticipantHostUserIds = null,
    IReadOnlyList<AppointmentGuestInput>? Guests = null);

public sealed record AppointmentParticipantDto(
    string UserId,
    string DisplayName,
    string? TitleName,
    bool IsPrimary)
{
    public string DisplayNameWithTitle => string.IsNullOrWhiteSpace(TitleName)
        ? DisplayName
        : $"{DisplayName} ({TitleName})";
}

public sealed record AppointmentGuestOutcomeDto(
    Attendance Attendance,
    string? Summary,
    bool WillRepeat,
    string? PostNotes,
    bool IsClosed,
    DateTime? ClosedAtLocal,
    string? ClosedByName,
    DateTime UpdatedAtLocal)
{
    public string AttendanceText => Attendance switch
    {
        Attendance.Arrived => "Geldi",
        Attendance.NoShow => "Gelmedi",
        Attendance.Postponed => "Ertelendi",
        Attendance.Cancelled => "İptal",
        _ => "-"
    };

    public string RepeatText => WillRepeat ? "Evet" : "Hayır";
}

public sealed record AppointmentGuestDto(
    string GuestId,
    string PersonId,
    string FirstName,
    string LastName,
    string PhoneE164,
    bool IsPrimary,
    AppointmentGuestOutcomeDto? Outcome)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed record AppointmentConflictDto(
    string AppointmentId,
    string PersonName,
    DateTime StartsAtLocal,
    DateTime EndsAtLocal,
    string? CategoryName,
    bool IsSamePerson,
    ConflictKind Kind = ConflictKind.Host,
    string? ResourceName = null);

public sealed record AppointmentSaveResult(
    bool Success,
    string? Error,
    string? AppointmentId,
    IReadOnlyList<AppointmentConflictDto>? Conflicts)
{
    /// <summary>Sunucu bağlantısı olmadığı için işlem kuyruğa alındı.</summary>
    public bool Queued { get; init; }

    /// <summary>Kayıt, istemcinin gördüğünden sonra sunucuda değişti.</summary>
    public bool VersionConflict { get; init; }

    /// <summary>Çakışmada sunucudaki güncel kayıt.</summary>
    public AppointmentDetailDto? CurrentAppointment { get; init; }

    public static AppointmentSaveResult Ok(string appointmentId)
        => new(true, null, appointmentId, null);

    public static AppointmentSaveResult QueuedOk(string appointmentId)
        => new(true, null, appointmentId, null) { Queued = true };

    public static AppointmentSaveResult Fail(string error)
        => new(false, error, null, null);

    public static AppointmentSaveResult Conflict(
        string error,
        IReadOnlyList<AppointmentConflictDto> conflicts)
        => new(false, error, null, conflicts);

    public static AppointmentSaveResult VersionConflictResult(
        string error,
        AppointmentDetailDto current)
        => new(false, error, null, null)
        {
            VersionConflict = true,
            CurrentAppointment = current,
        };
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
    string? StudentFullName,
    string? RoomId = null,
    string? RoomName = null,
    string? HostTitleName = null,
    int ParticipantCount = 0,
    int GuestCount = 0,
    string? ParticipantNames = null,
    string? GuestNames = null)
{
    public string PersonFullName => $"{PersonFirstName} {PersonLastName}".Trim();

    public string HostNameWithTitle => string.IsNullOrWhiteSpace(HostTitleName)
        ? HostName
        : $"{HostName} ({HostTitleName})";

    public string GuestSummaryText => GuestCount > 1
        ? $"{PersonFullName} +{GuestCount - 1} misafir"
        : PersonFullName;

    public string ParticipantSummaryText => ParticipantCount > 1
        ? $"{HostName} +{ParticipantCount - 1} görüşmeci"
        : HostName;

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

/// <summary>
/// Toplu taşıma sonucu. Çakışan görüşmeler taşınmaz; kullanıcı onaylarsa yalnızca
/// <see cref="ConflictingIds"/> ile tekrar çağrılır.
/// </summary>
public sealed record BulkMoveResult(
    int Moved,
    IReadOnlyList<string> ConflictingIds,
    IReadOnlyList<AppointmentConflictDto> Conflicts,
    IReadOnlyList<string> FailedIds,
    string? Error = null)
{
    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>Ağ/oturum yokluğu nedeniyle kuyruğa alınan taşıma sayısı.</summary>
    public int Queued { get; init; }
}

/// <summary>Toplu silme sonucu.</summary>
public sealed record BulkDeleteResult(
    int Deleted,
    IReadOnlyList<string> FailedIds,
    string? Error = null)
{
    /// <summary>Ağ/oturum yokluğu nedeniyle kuyruğa alınan silme sayısı.</summary>
    public int Queued { get; init; }
}

public sealed record DeletedAppointmentDto(
    string Id,
    DateTime StartsAtLocal,
    string PersonFullName,
    string PhoneE164,
    string HostName,
    string CategoryName,
    DateTime DeletedAtLocal)
{
    public const int RestoreWindowDays = 30;

    public string DateLabel => StartsAtLocal.ToString("dd/MM/yyyy");

    public string TimeLabel => StartsAtLocal.ToString("HH:mm");

    public string DeletedAtText => DeletedAtLocal.ToString("dd/MM/yyyy HH:mm");

    public int RemainingDays
        => Math.Max(0, RestoreWindowDays - (DateTime.Today - DeletedAtLocal.Date).Days);

    public string RemainingText => RemainingDays == 0
        ? "Son gün"
        : $"{RemainingDays} gün";
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
    DateTime? ClosedAtLocal,
    string? SeriesId = null,
    int? SeriesIndex = null,
    int? SeriesCount = null,
    DateTime UpdatedAtUtc = default,
    string? RoomId = null,
    string? RoomName = null,
    MeetingRoomKind? RoomKind = null,
    IReadOnlyList<AppointmentParticipantDto>? Participants = null,
    IReadOnlyList<AppointmentGuestDto>? Guests = null)
{
    public bool HasOutcome => Outcome is not null;

    public bool HasSeries => !string.IsNullOrWhiteSpace(SeriesId);

    public bool HasMultipleGuests => Guests is { Count: > 1 };

    public bool HasParticipants => Participants is { Count: > 1 };

    public string HostNameWithTitle
    {
        get
        {
            var primary = Participants?.FirstOrDefault(x => x.IsPrimary);
            return primary is null
                ? HostName
                : primary.DisplayNameWithTitle;
        }
    }

    public string RoomDisplayName => string.IsNullOrWhiteSpace(RoomName) ? "-" : RoomName;

    public string GuestSummaryText => Guests is { Count: > 1 }
        ? $"{PersonFullName} +{Guests.Count - 1} misafir"
        : PersonFullName;

    public string ParticipantSummaryText => Participants is { Count: > 1 }
        ? $"{HostName} +{Participants.Count - 1} görüşmeci"
        : HostName;

    public string SeriesText => HasSeries
        ? $"Seri {SeriesIndex ?? 0}/{SeriesCount ?? 0}"
        : "Tek görüşme";

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
