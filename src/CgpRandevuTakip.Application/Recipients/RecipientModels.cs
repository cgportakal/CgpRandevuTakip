namespace CgpRandevuTakip.Application.Recipients;

public sealed record RecipientDto(
    string Id,
    string Name,
    string PhoneE164,
    string? Tag,
    bool IsActive)
{
    public string PhoneDisplay => PhoneE164;
}

public sealed record RecipientInput(
    string Name,
    string Phone,
    string? Tag,
    bool IsActive);
