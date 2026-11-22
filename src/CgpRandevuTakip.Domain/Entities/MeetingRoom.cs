using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>
/// Fiziksel görüşme odası. Her görüşmecinin kendi odası ve üç ortak oda bulunur.
/// </summary>
public class MeetingRoom : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public MeetingRoomKind Kind { get; set; } = MeetingRoomKind.Shared;
    public string? HostUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Ad, host adından otomatik üretildiyse true; admin elle değiştirirse false.</summary>
    public bool AutoNamed { get; set; } = true;

    public User? HostUser { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
