namespace CgpRandevuTakip.Domain.Entities;

/// <summary>
/// Randevuya katılan görüşmeci. <see cref="Appointment.HostUserId"/> birincil
/// görüşmeciyi taşır; bu tablo birincil dâhil tüm katılımcıları tutar.
/// </summary>
public class AppointmentHost
{
    public string AppointmentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Appointment? Appointment { get; set; }
    public User? User { get; set; }
}
