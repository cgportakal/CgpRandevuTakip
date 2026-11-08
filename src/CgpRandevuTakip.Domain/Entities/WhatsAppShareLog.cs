using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

public class WhatsAppShareLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? AppointmentId { get; set; }
    public DateTime ShareDate { get; set; }
    public string RecipientIdsJson { get; set; } = "[]";
    public ShareMethod Method { get; set; }
    public ShareResult Result { get; set; }
    public string? ImagePath { get; set; }
    public string? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
