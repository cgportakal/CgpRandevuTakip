using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Appointments;

public sealed record GuestOutcomeInput(
    string GuestId,
    Attendance Attendance,
    string? Summary,
    bool WillRepeat,
    string? PostNotes,
    bool IsClosed);

public sealed record AppointmentOutcomeInput(
    Attendance Attendance,
    string? Summary,
    bool WillRepeat,
    string? PostNotes,
    bool IsClosed,
    IReadOnlyList<GuestOutcomeInput>? GuestOutcomes = null);

public sealed record AppointmentOutcomeDto(
    Attendance Attendance,
    string? Summary,
    bool WillRepeat,
    string? PostNotes,
    bool IsClosed,
    DateTime? ClosedAtLocal,
    string? ClosedByName,
    DateTime UpdatedAtLocal,
    IReadOnlyList<AppointmentGuestDto>? Guests = null)
{
    public string AttendanceText => Attendance switch
    {
        Attendance.Arrived => "Geldi",
        Attendance.NoShow => "Gelmedi",
        Attendance.Postponed => "Ertelendi",
        Attendance.Cancelled => "İptal",
        _ => "—"
    };

    public string RepeatText => WillRepeat ? "Evet" : "Hayır";

    public string SummaryText => string.IsNullOrWhiteSpace(Summary) ? "—" : Summary;

    public string PostNotesText => string.IsNullOrWhiteSpace(PostNotes) ? "—" : PostNotes;

    public string ClosedAtText => ClosedAtLocal is null
        ? "—"
        : ClosedAtLocal.Value.ToString("dd/MM/yyyy HH:mm");
}
