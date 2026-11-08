using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>Günlük liste paylaşımında seçilebilen kurum içi alıcı.</summary>
public class Recipient : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string PhoneE164 { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public bool IsActive { get; set; } = true;
}
