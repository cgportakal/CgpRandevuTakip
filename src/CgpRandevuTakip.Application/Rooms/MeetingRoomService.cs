using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Rooms;

public sealed class MeetingRoomService : IMeetingRoomService
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 120;

    private readonly IMeetingRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public MeetingRoomService(
        IMeetingRoomRepository rooms,
        IUserRepository users,
        IAuditLogger audit,
        ICurrentUser currentUser,
        IClock clock)
    {
        _rooms = rooms;
        _users = users;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MeetingRoomDto>> GetAllAsync()
    {
        var entities = await _rooms.GetAllAsync();
        var usage = await _rooms.GetUsageCountsAsync();
        return entities.Select(x => ToDto(x, usage)).ToList();
    }

    public async Task<IReadOnlyList<MeetingRoomDto>> GetActiveAsync(string? hostUserId = null)
    {
        var entities = await _rooms.GetActiveAsync(hostUserId);
        var usage = await _rooms.GetUsageCountsAsync();
        return entities.Select(x => ToDto(x, usage)).ToList();
    }

    public async Task<MeetingRoomDto?> GetByIdAsync(string id)
    {
        var room = await _rooms.GetByIdAsync(id);
        if (room is null)
            return null;

        var usage = await _rooms.GetUsageCountsAsync();
        return ToDto(room, usage);
    }

    public async Task<MeetingRoomDto?> GetHostRoomAsync(string hostUserId)
    {
        var room = await _rooms.GetHostRoomAsync(hostUserId);
        if (room is null)
            return null;

        var usage = await _rooms.GetUsageCountsAsync();
        return ToDto(room, usage);
    }

    public async Task EnsureHostRoomAsync(string hostUserId, string displayName)
    {
        var room = await _rooms.GetHostRoomAsync(hostUserId);
        if (room is null)
        {
            await _rooms.AddAsync(new MeetingRoom
            {
                Name = BuildHostRoomName(displayName),
                Kind = MeetingRoomKind.HostRoom,
                HostUserId = hostUserId,
                IsActive = true,
                SortOrder = 100,
                AutoNamed = true
            });
            return;
        }

        if (!room.AutoNamed)
            return;

        var expected = BuildHostRoomName(displayName);
        if (string.Equals(room.Name, expected, StringComparison.Ordinal))
            return;

        room.Name = expected;
        await _rooms.SaveAsync(room);
    }

    public async Task SetHostRoomActiveAsync(string hostUserId, bool isActive)
    {
        var room = await _rooms.GetHostRoomAsync(hostUserId);
        if (room is null || room.IsActive == isActive)
            return;

        room.IsActive = isActive;
        await _rooms.SaveAsync(room);
    }

    public async Task<OperationResult> CreateAsync(MeetingRoomInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Oda ekleme yetkisi yalnızca yöneticidedir.");

        var name = Normalize(input.Name);
        var validation = Validate(name);
        if (validation is not null)
            return OperationResult.Fail(validation);

        if (!Enum.IsDefined(input.Kind))
            return OperationResult.Fail("Geçersiz oda türü.");

        if (await _rooms.NameExistsAsync(name))
            return OperationResult.Fail("Bu adda bir oda zaten var.");

        string? hostUserId = null;
        var autoNamed = false;

        if (input.Kind == MeetingRoomKind.HostRoom)
        {
            hostUserId = input.HostUserId?.Trim();
            if (string.IsNullOrWhiteSpace(hostUserId))
                return OperationResult.Fail("Görüşmeci odası için görüşmeci seçilmelidir.");

            var host = await _users.GetByIdAsync(hostUserId);
            if (host is null)
                return OperationResult.Fail("Görüşmeci bulunamadı.");

            if (await _rooms.GetHostRoomAsync(hostUserId) is not null)
                return OperationResult.Fail("Bu görüşmecinin zaten bir odası var.");
        }

        var room = new MeetingRoom
        {
            Name = name,
            Kind = input.Kind,
            HostUserId = hostUserId,
            SortOrder = input.SortOrder == 0
                ? await _rooms.GetMaxSortOrderAsync(input.Kind) + 1
                : input.SortOrder,
            IsActive = input.IsActive,
            AutoNamed = autoNamed
        };

        await _rooms.AddAsync(room);
        await _audit.LogAsync(
            AuditAction.MeetingRoomCreated,
            _currentUser.UserId,
            nameof(MeetingRoom),
            room.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(string id, MeetingRoomInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Oda düzenleme yetkisi yalnızca yöneticidedir.");

        var room = await _rooms.GetByIdAsync(id);
        if (room is null)
            return OperationResult.Fail("Oda bulunamadı.");

        var name = Normalize(input.Name);
        var validation = Validate(name);
        if (validation is not null)
            return OperationResult.Fail(validation);

        if (await _rooms.NameExistsAsync(name, id))
            return OperationResult.Fail("Bu adda bir oda zaten var.");

        room.Name = name;
        room.SortOrder = input.SortOrder;
        room.IsActive = input.IsActive;
        room.AutoNamed = false;
        await _rooms.SaveAsync(room);

        await _audit.LogAsync(
            input.IsActive ? AuditAction.MeetingRoomUpdated : AuditAction.MeetingRoomDeactivated,
            _currentUser.UserId,
            nameof(MeetingRoom),
            room.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetActiveAsync(string id, bool isActive)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Oda durumunu yalnızca yönetici değiştirebilir.");

        var room = await _rooms.GetByIdAsync(id);
        if (room is null)
            return OperationResult.Fail("Oda bulunamadı.");

        if (room.IsActive == isActive)
            return OperationResult.Ok();

        if (!isActive)
        {
            var upcoming = await _rooms.CountUpcomingAsync(id, _clock.UtcNow);
            if (upcoming > 0)
                return OperationResult.Fail(
                    $"Bu odada {upcoming} gelecek randevu var. Önce onları taşıyın veya iptal edin.");
        }

        room.IsActive = isActive;
        await _rooms.SaveAsync(room);

        await _audit.LogAsync(
            isActive ? AuditAction.MeetingRoomUpdated : AuditAction.MeetingRoomDeactivated,
            _currentUser.UserId,
            nameof(MeetingRoom),
            room.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(string id)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Oda silme yetkisi yalnızca yöneticidedir.");

        var room = await _rooms.GetByIdAsync(id);
        if (room is null)
            return OperationResult.Fail("Oda bulunamadı.");

        if (room.Kind == MeetingRoomKind.HostRoom)
            return OperationResult.Fail("Görüşmeci odası silinemez; pasifleştirebilirsiniz.");

        var usage = await _rooms.GetUsageCountsAsync();
        if (usage.TryGetValue(id, out var count) && count > 0)
            return OperationResult.Fail(
                $"Bu odada {count} randevu var. Oda silinemez; pasifleştirebilirsiniz.");

        await _rooms.DeleteAsync(room);
        await _audit.LogAsync(
            AuditAction.MeetingRoomDeactivated,
            _currentUser.UserId,
            nameof(MeetingRoom),
            room.Id);

        return OperationResult.Ok();
    }

    private bool IsAdmin() => _currentUser.IsInRole(UserRole.Admin);

    private static string BuildHostRoomName(string displayName)
    {
        var trimmed = displayName.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "Görüşme Odası" : $"{trimmed} Odası";
    }

    private static string Normalize(string? name) => name?.Trim() ?? string.Empty;

    private static string? Validate(string name)
    {
        if (name.Length < MinNameLength)
            return $"Oda adı en az {MinNameLength} karakter olmalıdır.";

        return name.Length > MaxNameLength
            ? $"Oda adı en fazla {MaxNameLength} karakter olabilir."
            : null;
    }

    private static MeetingRoomDto ToDto(
        MeetingRoom room,
        IReadOnlyDictionary<string, int> usage)
        => new(
            room.Id,
            room.Name,
            room.Kind,
            room.HostUserId,
            room.HostUser?.DisplayName,
            room.IsActive,
            room.SortOrder,
            usage.TryGetValue(room.Id, out var count) ? count : 0);
}
