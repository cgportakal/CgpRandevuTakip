using System.Text.RegularExpressions;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Categories;

public sealed class CategoryService : ICategoryService
{
    public const int MaxNameLength = 50;
    private const string DefaultColor = "#2E7D5B";
    private static readonly Regex ColorPattern = new(
        "^#?[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ICategoryRepository _categories;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;

    public CategoryService(
        ICategoryRepository categories,
        IAuditLogger audit,
        ICurrentUser currentUser)
    {
        _categories = categories;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync()
    {
        var entities = await _categories.GetAllAsync();
        var usage = await _categories.GetUsageCountsAsync();
        return entities.Select(x => ToDto(x, usage)).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveAsync()
    {
        var entities = await _categories.GetActiveAsync();
        var usage = await _categories.GetUsageCountsAsync();
        return entities.Select(x => ToDto(x, usage)).ToList();
    }

    public async Task<OperationResult> CreateAsync(CategoryInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Bu işlem için yönetici yetkisi gerekir.");

        var validation = Validate(input, out var name, out var color);
        if (validation is not null)
            return OperationResult.Fail(validation);

        if (await _categories.NameExistsAsync(name))
            return OperationResult.Fail("Bu adla bir kategori zaten var.");

        var category = new Category
        {
            Name = name,
            ColorHex = color,
            SortOrder = await _categories.GetMaxSortOrderAsync() + 1,
            IsActive = input.IsActive,
            IsDeleted = false
        };

        await _categories.AddAsync(category);
        await _audit.LogAsync(AuditAction.CategoryCreated, _currentUser.UserId, nameof(Category), category.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(string id, CategoryInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Bu işlem için yönetici yetkisi gerekir.");

        var category = await _categories.GetByIdAsync(id);
        if (category is null)
            return OperationResult.Fail("Kategori bulunamadı.");

        var validation = Validate(input, out var name, out var color);
        if (validation is not null)
            return OperationResult.Fail(validation);

        if (await _categories.NameExistsAsync(name, id))
            return OperationResult.Fail("Bu adla bir kategori zaten var.");

        category.Name = name;
        category.ColorHex = color;
        category.IsActive = input.IsActive;
        await _categories.SaveAsync(category);

        await _audit.LogAsync(AuditAction.CategoryUpdated, _currentUser.UserId, nameof(Category), category.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetActiveAsync(string id, bool isActive)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Bu işlem için yönetici yetkisi gerekir.");

        var category = await _categories.GetByIdAsync(id);
        if (category is null)
            return OperationResult.Fail("Kategori bulunamadı.");

        if (category.IsActive == isActive)
            return OperationResult.Ok();

        category.IsActive = isActive;
        await _categories.SaveAsync(category);

        var action = isActive ? AuditAction.CategoryUpdated : AuditAction.CategoryDeactivated;
        await _audit.LogAsync(action, _currentUser.UserId, nameof(Category), category.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(string id)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Bu işlem için yönetici yetkisi gerekir.");

        var category = await _categories.GetByIdAsync(id);
        if (category is null)
            return OperationResult.Fail("Kategori bulunamadı.");

        var usage = await _categories.GetUsageCountsAsync();
        if (usage.TryGetValue(id, out var count) && count > 0)
            return OperationResult.Fail(
                $"Bu kategori {count} görüşmede kullanılıyor. Silmek yerine pasifleştirin.");

        await _categories.DeleteAsync(category);
        await _audit.LogAsync(AuditAction.CategoryDeleted, _currentUser.UserId, nameof(Category), category.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> MoveAsync(string id, int direction)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Bu işlem için yönetici yetkisi gerekir.");

        if (direction is not (-1 or 1))
            return OperationResult.Fail("Geçersiz sıralama yönü.");

        var list = (await _categories.GetAllAsync()).ToList();
        var index = list.FindIndex(x => x.Id == id);
        if (index < 0)
            return OperationResult.Fail("Kategori bulunamadı.");

        var target = index + direction;
        if (target < 0 || target >= list.Count)
            return OperationResult.Ok();

        var item = list[index];
        list.RemoveAt(index);
        list.Insert(target, item);

        for (var i = 0; i < list.Count; i++)
            list[i].SortOrder = i;

        await _categories.SaveRangeAsync(list);
        await _audit.LogAsync(AuditAction.CategoryUpdated, _currentUser.UserId, nameof(Category), id);

        return OperationResult.Ok();
    }

    private bool IsAdmin() => _currentUser.IsInRole(UserRole.Admin);

    private static string? Validate(CategoryInput input, out string name, out string color)
    {
        name = input.Name?.Trim() ?? string.Empty;
        color = string.IsNullOrWhiteSpace(input.ColorHex)
            ? DefaultColor
            : input.ColorHex.Trim().ToUpperInvariant();

        if (name.Length == 0)
            return "Kategori adı zorunludur.";

        if (name.Length > MaxNameLength)
            return $"Kategori adı en fazla {MaxNameLength} karakter olabilir.";

        if (!ColorPattern.IsMatch(color))
            return "Renk #RRGGBB biçiminde olmalıdır (ör. #2E7D5B).";

        if (!color.StartsWith('#'))
            color = "#" + color;

        return null;
    }

    private static CategoryDto ToDto(Category category, IReadOnlyDictionary<string, int> usage)
        => new(
            category.Id,
            category.Name,
            category.ColorHex,
            category.SortOrder,
            category.IsActive,
            usage.TryGetValue(category.Id, out var count) ? count : 0);
}
