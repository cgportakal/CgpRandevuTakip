using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
