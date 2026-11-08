using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#2E7D5B";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
