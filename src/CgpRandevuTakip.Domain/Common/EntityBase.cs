namespace CgpRandevuTakip.Domain.Common;

/// <summary>Tüm kalıcı varlıkların ortak kimlik ve oluşturma bilgisi.</summary>
public abstract class EntityBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
