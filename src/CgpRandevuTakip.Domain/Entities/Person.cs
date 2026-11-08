using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>Görüşmeye gelen kişi (veli, öğrenci, başvuru sahibi).</summary>
public class Person : EntityBase
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneE164 { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Organization { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
