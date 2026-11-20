using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Desktop.Services.Api;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>
/// Görüşmeci listeleri sunucu manifestinden okunur; manifest alınamazsa yerel
/// kullanıcılara düşer. Düzenleme işlemleri P3'e kadar yerelde kalır.
/// </summary>
public sealed class SyncedHostService : IHostService
{
    private readonly IHostService _local;
    private readonly ServerReadService _reads;

    public SyncedHostService(IHostService local, ServerReadService reads)
    {
        _local = local;
        _reads = reads;
    }

    public async Task<IReadOnlyList<HostDto>> GetAllAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest?.Hosts ?? await _local.GetAllAsync();
    }

    public async Task<IReadOnlyList<HostDto>> GetSelectableHostsAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest is null
            ? await _local.GetSelectableHostsAsync()
            : manifest.Hosts.Where(x => x.IsActive).ToList();
    }

    public Task<OperationResult> CreateAsync(HostCreateInput input) => _local.CreateAsync(input);

    public Task<OperationResult> UpdateAsync(string id, HostUpdateInput input) => _local.UpdateAsync(id, input);

    private async Task<ApiManifest?> LoadManifestAsync()
    {
        if (!await _reads.IsServerReadActiveAsync())
            return null;

        var result = await _reads.GetManifestAsync();
        return result.Success ? result.Data : null;
    }
}
