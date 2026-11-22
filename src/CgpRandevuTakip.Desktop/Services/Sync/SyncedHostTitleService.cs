using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Titles;
using CgpRandevuTakip.Desktop.Services.Api;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>Ünvan listesi sunucu manifestinden okunur; yazma işlemleri yerelde kalır.</summary>
public sealed class SyncedHostTitleService : IHostTitleService
{
    private readonly IHostTitleService _local;
    private readonly ServerReadService _reads;

    public SyncedHostTitleService(IHostTitleService local, ServerReadService reads)
    {
        _local = local;
        _reads = reads;
    }

    public async Task<IReadOnlyList<HostTitleDto>> GetAllAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest?.Titles ?? await _local.GetAllAsync();
    }

    public async Task<IReadOnlyList<HostTitleDto>> GetActiveAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest is null
            ? await _local.GetActiveAsync()
            : manifest.Titles?.Where(x => x.IsActive).ToList()
              ?? await _local.GetActiveAsync();
    }

    public Task<OperationResult> CreateAsync(HostTitleInput input) => _local.CreateAsync(input);

    public Task<OperationResult> UpdateAsync(string id, HostTitleInput input) =>
        _local.UpdateAsync(id, input);

    public Task<OperationResult> SetActiveAsync(string id, bool isActive) =>
        _local.SetActiveAsync(id, isActive);

    public Task<OperationResult> DeleteAsync(string id) => _local.DeleteAsync(id);

    private async Task<ApiManifest?> LoadManifestAsync()
    {
        if (!await _reads.IsServerReadActiveAsync())
            return null;

        var result = await _reads.GetManifestAsync();
        return result.Success ? result.Data : null;
    }
}
