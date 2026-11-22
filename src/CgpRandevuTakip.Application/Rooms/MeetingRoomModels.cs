using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Rooms;

public sealed record MeetingRoomDto(
    string Id,
    string Name,
    MeetingRoomKind Kind,
    string? HostUserId,
    string? HostName,
    bool IsActive,
    int SortOrder,
    int AppointmentCount)
{
    public bool IsHostRoom => Kind == MeetingRoomKind.HostRoom;

    public string KindText => IsHostRoom ? "Görüşmeci odası" : "Ortak oda";

    public string DisplayName => IsHostRoom && !string.IsNullOrWhiteSpace(HostName)
        ? $"{Name} ({HostName})"
        : Name;
}

public sealed record MeetingRoomInput(
    string Name,
    MeetingRoomKind Kind,
    string? HostUserId,
    int SortOrder,
    bool IsActive);

public interface IMeetingRoomService
{
    Task<IReadOnlyList<MeetingRoomDto>> GetAllAsync();
    Task<IReadOnlyList<MeetingRoomDto>> GetActiveAsync(string? hostUserId = null);
    Task<MeetingRoomDto?> GetByIdAsync(string id);
    Task<MeetingRoomDto?> GetHostRoomAsync(string hostUserId);

    /// <summary>Host odası yoksa oluşturur; otomatik adlandırılmışsa adını günceller.</summary>
    Task EnsureHostRoomAsync(string hostUserId, string displayName);

    /// <summary>Host pasifleşince kendi odasını da pasifleştirir; aktifleşince geri açar.</summary>
    Task SetHostRoomActiveAsync(string hostUserId, bool isActive);

    Task<OperationResult> CreateAsync(MeetingRoomInput input);
    Task<OperationResult> UpdateAsync(string id, MeetingRoomInput input);
    Task<OperationResult> SetActiveAsync(string id, bool isActive);
    Task<OperationResult> DeleteAsync(string id);
}
