using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Persistence;

/// <summary>
/// Görüşme odaları, ünvanlar, katılımcılar ve misafirler için idempotent başlangıç/backfill.
/// </summary>
public static partial class DatabaseInitializer
{
    private static readonly string[] DefaultSharedRoomNames =
    {
        "Oda 1",
        "Oda 2",
        "Oda 3"
    };

    /// <summary>Üç ortak oda ve her görüşmecinin kendi odası yoksa oluşturur.</summary>
    private static void EnsureMeetingRooms(AppDbContext db)
    {
        var sharedChanged = false;
        for (var i = 0; i < DefaultSharedRoomNames.Length; i++)
        {
            var name = DefaultSharedRoomNames[i];
            var exists = db.MeetingRooms.Any(x =>
                x.Kind == MeetingRoomKind.Shared && x.Name == name);

            if (exists)
                continue;

            db.MeetingRooms.Add(new MeetingRoom
            {
                Name = name,
                Kind = MeetingRoomKind.Shared,
                IsActive = true,
                SortOrder = i,
                AutoNamed = false
            });
            sharedChanged = true;
        }

        if (sharedChanged)
            db.SaveChanges();

        var hostIds = db.Users
            .Where(x => x.IsActive && x.Role == UserRole.Host)
            .Select(x => x.Id)
            .ToList();

        hostIds.AddRange(db.Appointments
            .Select(x => x.HostUserId)
            .Distinct()
            .ToList());

        foreach (var hostId in hostIds.Distinct())
        {
            EnsureHostRoom(db, hostId);
        }

        db.SaveChanges();
    }

    /// <summary>Bir görüşmecinin kendi odasını döndürür; yoksa oluşturur.</summary>
    private static MeetingRoom EnsureHostRoom(AppDbContext db, string hostUserId)
    {
        var existing = db.MeetingRooms.FirstOrDefault(x =>
            x.Kind == MeetingRoomKind.HostRoom && x.HostUserId == hostUserId);

        if (existing is not null)
            return existing;

        var host = db.Users.FirstOrDefault(x => x.Id == hostUserId);
        var name = host is null
            ? $"Görüşme Odası {hostUserId[..Math.Min(6, hostUserId.Length)]}"
            : $"{host.DisplayName} Odası";

        var room = new MeetingRoom
        {
            Name = name,
            Kind = MeetingRoomKind.HostRoom,
            HostUserId = hostUserId,
            IsActive = true,
            SortOrder = 100,
            AutoNamed = true
        };

        db.MeetingRooms.Add(room);
        db.SaveChanges();
        return room;
    }

    /// <summary>
    /// Eski randevulara oda, birincil katılımcı ve birincil misafir satırlarını ekler.
    /// Mevcut sonuç verisi birincil misafire kopyalanır.
    /// </summary>
    private static void BackfillMeetingRelations(AppDbContext db)
    {
        var appointments = db.Appointments
            .Include(x => x.Outcome)
            .Include(x => x.Participants)
            .Include(x => x.Guests)
            .Where(x => x.RoomId == null
                        || !x.Participants.Any()
                        || !x.Guests.Any())
            .ToList();

        if (appointments.Count == 0)
            return;

        var userIds = db.Users.Select(x => x.Id).ToHashSet();
        var personIds = db.Persons.Select(x => x.Id).ToHashSet();

        foreach (var appointment in appointments)
        {
            var hostExists = userIds.Contains(appointment.HostUserId);

            if (appointment.RoomId is null && hostExists)
            {
                appointment.RoomId = EnsureHostRoom(db, appointment.HostUserId).Id;
            }

            if (hostExists && appointment.Participants.Count == 0)
            {
                appointment.Participants.Add(new AppointmentHost
                {
                    AppointmentId = appointment.Id,
                    UserId = appointment.HostUserId,
                    IsPrimary = true,
                    SortOrder = 0
                });
            }

            if (personIds.Contains(appointment.PersonId) && appointment.Guests.Count == 0)
            {
                var guest = new AppointmentGuest
                {
                    AppointmentId = appointment.Id,
                    PersonId = appointment.PersonId,
                    IsPrimary = true,
                    SortOrder = 0
                };

                if (appointment.Outcome is { } outcome)
                {
                    guest.Attendance = outcome.Attendance;
                    guest.Summary = outcome.Summary;
                    guest.PostNotes = outcome.PostNotes;
                    guest.WillRepeat = outcome.WillRepeat;
                    guest.IsClosed = outcome.IsClosed;
                    guest.ClosedAtUtc = outcome.ClosedAtUtc;
                    guest.UpdatedAtUtc = outcome.UpdatedAtUtc;
                    guest.UpdatedByUserId = outcome.UpdatedByUserId;
                }

                appointment.Guests.Add(guest);
            }
        }

        db.SaveChanges();
    }
}
