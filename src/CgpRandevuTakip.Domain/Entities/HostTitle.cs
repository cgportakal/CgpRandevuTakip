using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>
/// Görüşmecilere atanan ünvan. Yönetilen liste hâlinde tutulur; admin tanımlar.
/// </summary>
public class HostTitle : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();
}
