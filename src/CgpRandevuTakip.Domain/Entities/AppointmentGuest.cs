using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>
/// Randevuya gelen misafir. Bir randevuda birden fazla misafir olabilir; her misafirin
/// katılım ve değerlendirmesi bağımsız tutulur. <see cref="Appointment.PersonId"/>
/// birincil misafiri taşır.
/// </summary>
public class AppointmentGuest : EntityBase
{
    public string AppointmentId { get; set; } = string.Empty;
    public string PersonId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }

    public Attendance? Attendance { get; set; }
    public string? Summary { get; set; }
    public string? PostNotes { get; set; }
    public bool WillRepeat { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }

    public Appointment? Appointment { get; set; }
    public Person? Person { get; set; }
}
