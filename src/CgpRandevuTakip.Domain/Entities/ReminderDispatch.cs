using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

public class ReminderDispatch : EntityBase
{
    public string AppointmentId { get; set; } = string.Empty;

    public ReminderKind Kind { get; set; } = ReminderKind.Appointment;

    /// <summary>Randevudan kaç dakika önce: 1440, 60, 30 veya (arama için) 5.</summary>
    public int OffsetMinutes { get; set; }

    public DateTime DueAtUtc { get; set; }
    public ReminderState State { get; set; } = ReminderState.Pending;
    public ReminderAction LastAction { get; set; } = ReminderAction.None;

    public int RepeatCount { get; set; }
    public int SnoozeCount { get; set; }

    public DateTime? WindowsNotifiedAtUtc { get; set; }
    public DateTime? PushNotifiedAtUtc { get; set; }
    public DateTime? ActionAtUtc { get; set; }
    public string? ActionByUserId { get; set; }

    /// <summary>Idempotency anahtarı: aynı randevu + aralık + nesil için tek kayıt.</summary>
    public string DedupKey { get; set; } = string.Empty;

    public Appointment? Appointment { get; set; }
}
