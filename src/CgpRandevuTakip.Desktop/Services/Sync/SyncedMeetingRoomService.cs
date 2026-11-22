using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Rooms;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Services.Sync;

/// <summary>Oda listesi sunucu manifestinden okunur; yazma işlemleri yerelde kalır.</summary>
public sealed class SyncedMeetingRoomService : IMeetingRoomService
{
    private readonly IMeetingRoomService _local;
    private readonly ServerReadService _reads;

    public SyncedMeetingRoomService(IMeetingRoomService local, ServerReadService reads)
    {
        _local = local;
        _reads = reads;
    }

    public async Task<IReadOnlyList<MeetingRoomDto>> GetAllAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest?.Rooms ?? await _local.GetAllAsync();
    }

    public async Task<IReadOnlyList<MeetingRoomDto>> GetActiveAsync(string? hostUserId = null)
    {
        var manifest = await LoadManifestAsync();
        if (manifest?.Rooms is null)
            return await _local.GetActiveAsync(hostUserId);

        return manifest.Rooms
            .Where(x => x.IsActive
                        && (hostUserId is null
                            || x.Kind == MeetingRoomKind.Shared
                            || x.HostUserId == hostUserId))
            .ToList();
    }

    public async Task<MeetingRoomDto?> GetByIdAsync(string id)
    {
        var manifest = await LoadManifestAsync();
        return manifest?.Rooms?.FirstOrDefault(x => x.Id == id)
               ?? await _local.GetByIdAsync(id);
    }

    public async Task<MeetingRoomDto?> GetHostRoomAsync(string hostUserId)
    {
        var manifest = await LoadManifestAsync();
        return manifest?.Rooms?.FirstOrDefault(
                   x => x.Kind == MeetingRoomKind.HostRoom
                        && x.HostUserId == hostUserId)
               ?? await _local.GetHostRoomAsync(hostUserId);
    }

    public Task EnsureHostRoomAsync(string hostUserId, string displayName) =>
        _local.EnsureHostRoomAsync(hostUserId, displayName);

    public Task SetHostRoomActiveAsync(string hostUserId, bool isActive) =>
        _local.SetHostRoomActiveAsync(hostUserId, isActive);

    public Task<OperationResult> CreateAsync(MeetingRoomInput input) => _local.CreateAsync(input);

    public Task<OperationResult> UpdateAsync(string id, MeetingRoomInput input) =>
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
