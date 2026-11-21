using CgpRandevuTakip.Domain.Common;

namespace CgpRandevuTakip.Domain.Entities;

/// <summary>
/// Sunucuya gönderilemeyen yazma isteklerinin kuyruğu. Kayıtlar oluşturulma
/// sırasıyla gönderilir; başarılı olanlar silinir.
/// </summary>
public sealed class SyncOutboxItem : EntityBase
{
    public string Operation { get; set; } = string.Empty;

    /// <summary>Sunucu kimliği veya oluşturma isteğinde istemci istek kimliği.</summary>
    public string EntityId { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>pending / failed</summary>
    public string State { get; set; } = "pending";
}
