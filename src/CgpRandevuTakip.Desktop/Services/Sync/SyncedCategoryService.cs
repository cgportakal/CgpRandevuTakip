using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Desktop.Services.Api;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Kategori listeleri sunucu manifestinden okunur; manifest alınamazsa yerel
/// kategorilere düşer. Düzenleme işlemleri P3'e kadar yerelde kalır.
/// </summary>
public sealed class SyncedCategoryService : ICategoryService
{
    private readonly ICategoryService _local;
    private readonly ServerReadService _reads;

    public SyncedCategoryService(ICategoryService local, ServerReadService reads)
    {
        _local = local;
        _reads = reads;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest?.Categories ?? await _local.GetAllAsync();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest is null
            ? await _local.GetActiveAsync()
            : manifest.Categories.Where(x => x.IsActive).ToList();
    }

    public Task<OperationResult> CreateAsync(CategoryInput input) => _local.CreateAsync(input);

    public Task<OperationResult> UpdateAsync(string id, CategoryInput input) => _local.UpdateAsync(id, input);

    public Task<OperationResult> SetActiveAsync(string id, bool isActive) => _local.SetActiveAsync(id, isActive);

    public Task<OperationResult> DeleteAsync(string id) => _local.DeleteAsync(id);

    public Task<OperationResult> MoveAsync(string id, int direction) => _local.MoveAsync(id, direction);

    private async Task<ApiManifest?> LoadManifestAsync()
    {
        if (!await _reads.IsServerReadActiveAsync())
            return null;

        var result = await _reads.GetManifestAsync();
        return result.Success ? result.Data : null;
    }
}
