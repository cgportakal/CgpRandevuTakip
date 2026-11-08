namespace CgpRandevuTakip.Application.Categories;

public sealed record CategoryDto(
    string Id,
    string Name,
    string ColorHex,
    int SortOrder,
    bool IsActive,
    int UsageCount);

public sealed record CategoryInput(
    string Name,
    string ColorHex,
    bool IsActive);
