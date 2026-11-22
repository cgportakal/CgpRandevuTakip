using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Repositories;

public sealed class MeetingRoomRepository : IMeetingRoomRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public MeetingRoomRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<MeetingRoom>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MeetingRooms.AsNoTracking()
            .Include(x => x.HostUser)
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MeetingRoom>> GetActiveAsync(string? hostUserId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MeetingRooms.AsNoTracking()
            .Include(x => x.HostUser)
            .Where(x => x.IsActive
                        && (hostUserId == null
                            || x.Kind == MeetingRoomKind.Shared
                            || x.HostUserId == hostUserId))
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<MeetingRoom?> GetByIdAsync(string id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MeetingRooms.AsNoTracking()
            .Include(x => x.HostUser)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<MeetingRoom?> GetHostRoomAsync(string hostUserId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MeetingRooms.AsNoTracking()
            .Include(x => x.HostUser)
            .FirstOrDefaultAsync(x =>
                x.Kind == MeetingRoomKind.HostRoom && x.HostUserId == hostUserId);
    }

    public async Task<bool> NameExistsAsync(string name, string? excludeId = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MeetingRooms.AsNoTracking()
            .AnyAsync(x => x.Name == name && (excludeId == null || x.Id != excludeId));
    }

    public async Task<IReadOnlyDictionary<string, int>> GetUsageCountsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Appointments.AsNoTracking()
            .Where(x => x.RoomId != null)
            .GroupBy(x => x.RoomId!)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(x => x.RoomId, x => x.Count);
    }

    public async Task<int> CountUpcomingAsync(string roomId, DateTime nowUtc)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Appointments.AsNoTracking()
            .CountAsync(x => x.RoomId == roomId
                             && !x.IsDeleted
                             && !x.IsClosed
                             && x.StartsAtUtc >= nowUtc
                             && (x.Status == AppointmentStatus.Scheduled
                                 || x.Status == AppointmentStatus.Arrived
                                 || x.Status == AppointmentStatus.Postponed));
    }

    public async Task<int> GetMaxSortOrderAsync(MeetingRoomKind kind)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.MeetingRooms.AsNoTracking()
            .Where(x => x.Kind == kind)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? -1;
    }

    public async Task AddAsync(MeetingRoom room)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.MeetingRooms.Add(room);
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(MeetingRoom room)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.MeetingRooms.Update(room);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(MeetingRoom room)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.MeetingRooms.Remove(room);
        await db.SaveChangesAsync();
    }
}
