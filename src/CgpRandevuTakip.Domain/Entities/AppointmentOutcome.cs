using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>Görüşme sonucu; bir görüşmeyle bire bir ilişkilidir.</summary>
public class AppointmentOutcome
{
    public string AppointmentId { get; set; } = string.Empty;
    public Attendance Attendance { get; set; }
    public string? Summary { get; set; }
    public bool WillRepeat { get; set; }
    public string? PostNotes { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public Appointment? Appointment { get; set; }
}
