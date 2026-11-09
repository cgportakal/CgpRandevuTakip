using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Application;

public class AppointmentServiceTests
{
    // FixedClock: 2026-11-06 09:00 UTC · yerel saat UTC+3.
    private static readonly DateTime NowUtc = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required RecordingAuditLogger Audit { get; init; }
        public required AppointmentService Service { get; init; }
        public required User Host { get; init; }
        public required User OtherHost { get; init; }
        public required Category Category { get; init; }

        public void SignInAs(User user)
            => CurrentUser.SignIn(new UserSession(user.Id, user.Username, user.DisplayName, user.Role));

        public void SignInAsRole(UserRole role)
            => CurrentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));

        public AppointmentInput Input(
            string firstName = "Ali",
            string lastName = "Veli",
            string phone = "0532 123 45 67",
            DateTime? startsAtLocal = null,
            string? hostUserId = null,
            string? categoryId = null,
            IReadOnlyList<int>? offsets = null,
            bool ignoreConflicts = false)
            => new(
                firstName,
                lastName,
                phone,
                startsAtLocal ?? new DateTime(2026, 11, 8, 10, 0, 0),
                30,
                hostUserId ?? Host.Id,
                categoryId ?? Category.Id,
                AppointmentMethod.FaceToFace,
                null,
                null,
                null,
                offsets ?? Array.Empty<int>(),
                ignoreConflicts);

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();
        var users = new UserRepository(db.Factory);
        var categories = new CategoryRepository(db.Factory);

        var host = new User
        {
            Username = "host1",
            DisplayName = "Host Bir",
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        var otherHost = new User
        {
            Username = "host2",
            DisplayName = "Host İki",
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        var category = new Category { Name = "Veli Görüşmesi", ColorHex = "#2563EB" };

        await users.AddAsync(host);
        await users.AddAsync(otherHost);
        await categories.AddAsync(category);

        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("actor", "actor", "Aktör", UserRole.Secretary));
        var audit = new RecordingAuditLogger();

        var service = new AppointmentService(
            new AppointmentRepository(db.Factory),
            new PersonRepository(db.Factory),
            new UserRepository(db.Factory),
            new CategoryRepository(db.Factory),
            audit,
            new FixedClock(NowUtc),
            currentUser);

        return new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Audit = audit,
            Service = service,
            Host = host,
            OtherHost = otherHost,
            Category = category
        };
    }

    [Fact]
    public async Task Create_PersistsAppointmentAndFutureReminders()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(
            sut.Input(offsets: new[] { 1440, 60, 30 }));

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.AppointmentId);
        Assert.Contains(AuditAction.AppointmentCreated, sut.Audit.Actions);

        var detail = await sut.Service.GetDetailAsync(result.AppointmentId!);
        Assert.NotNull(detail);
        Assert.Equal("Ali Veli", detail!.PersonFullName);
        Assert.Equal("Host Bir", detail.HostName);
        Assert.Equal("Veli Görüşmesi", detail.CategoryName);
        Assert.Equal(30, detail.DurationMinutes);
        Assert.Equal(new[] { 1440, 60, 30 }, detail.ReminderOffsetsMinutes);

        using var db = sut.Db.CreateContext();
        var reminders = db.ReminderDispatches
            .Where(x => x.AppointmentId == result.AppointmentId)
            .ToList();

        Assert.Equal(4, reminders.Count);
        Assert.All(reminders, x => Assert.Equal(ReminderState.Pending, x.State));
        Assert.Equal(3, reminders.Count(x => x.Kind == ReminderKind.Appointment));
        Assert.Single(reminders, x => x.Kind == ReminderKind.Call && x.OffsetMinutes == 5);
        Assert.Equal(
            new DateTime(2026, 11, 7, 7, 0, 0, DateTimeKind.Utc),
            reminders.Single(x => x.Kind == ReminderKind.Appointment && x.OffsetMinutes == 1440).DueAtUtc);
    }

    [Fact]
    public async Task Create_SkipsRemindersThatFallInThePast()
    {
        using var sut = await CreateAsync();

        // Yerel 2026-11-06 13:00 → UTC 10:00; 1 gün önceki hatırlatıcı geçmişte kalır.
        var result = await sut.Service.CreateAsync(
            sut.Input(startsAtLocal: new DateTime(2026, 11, 6, 13, 0, 0),
                offsets: new[] { 1440, 30 }));

        Assert.True(result.Success, result.Error);

        using var db = sut.Db.CreateContext();
        var offsets = db.ReminderDispatches
            .Where(x => x.AppointmentId == result.AppointmentId
                        && x.Kind == ReminderKind.Appointment)
            .Select(x => x.OffsetMinutes)
            .ToList();

        Assert.Equal(new[] { 30 }, offsets);
    }

    [Fact]
    public async Task Create_NormalizesPhoneAndReusesPerson()
    {
        using var sut = await CreateAsync();

        var first = await sut.Service.CreateAsync(sut.Input(phone: "0532 123 45 67"));
        var second = await sut.Service.CreateAsync(
            sut.Input(phone: "+90 (532) 123 45 67", startsAtLocal: new DateTime(2026, 11, 8, 13, 0, 0)));

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);

        var detail = await sut.Service.GetDetailAsync(first.AppointmentId!);
        Assert.Equal("+905321234567", detail!.PhoneE164);

        using var db = sut.Db.CreateContext();
        Assert.Equal(1, db.Persons.Count());
    }

    [Fact]
    public async Task Create_RejectsInvalidPhone()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(sut.Input(phone: "abc"));

        Assert.False(result.Success);
        Assert.Contains("Telefon", result.Error);
    }

    [Fact]
    public async Task Create_RejectsUnknownHost()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateAsync(sut.Input(hostUserId: "yok-boyle-biri"));

        Assert.False(result.Success);
        Assert.Contains("host", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RequiresAppointmentManagementRole()
    {
        using var sut = await CreateAsync();
        sut.SignInAs(sut.Host);

        var result = await sut.Service.CreateAsync(sut.Input());

        Assert.False(result.Success);
        Assert.Contains("yetkiniz yok", result.Error);
    }

    [Fact]
    public async Task Create_DetectsHostConflictAndAllowsOverride()
    {
        using var sut = await CreateAsync();

        var first = await sut.Service.CreateAsync(sut.Input());
        Assert.True(first.Success, first.Error);

        var conflicting = await sut.Service.CreateAsync(
            sut.Input(firstName: "Zeynep", lastName: "Kaya", phone: "0555 111 22 33",
                startsAtLocal: new DateTime(2026, 11, 8, 10, 15, 0)));

        Assert.False(conflicting.Success);
        Assert.NotNull(conflicting.Conflicts);
        Assert.Single(conflicting.Conflicts!);
        Assert.False(conflicting.Conflicts![0].IsSamePerson);

        var forced = await sut.Service.CreateAsync(
            sut.Input(firstName: "Zeynep", lastName: "Kaya", phone: "0555 111 22 33",
                startsAtLocal: new DateTime(2026, 11, 8, 10, 15, 0),
                ignoreConflicts: true));

        Assert.True(forced.Success, forced.Error);
    }

    [Fact]
    public async Task Create_FlagsSamePersonConflict()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(sut.Input());
        var duplicate = await sut.Service.CreateAsync(
            sut.Input(startsAtLocal: new DateTime(2026, 11, 8, 10, 10, 0)));

        Assert.False(duplicate.Success);
        Assert.NotNull(duplicate.Conflicts);
        Assert.True(duplicate.Conflicts![0].IsSamePerson);
        Assert.Contains("Aynı kişi", duplicate.Error);
    }

    [Fact]
    public async Task HostRole_SeesOnlyOwnAppointments()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(sut.Input(startsAtLocal: new DateTime(2026, 11, 8, 10, 0, 0)));
        await sut.Service.CreateAsync(sut.Input(
            firstName: "Zeynep", phone: "0555 111 22 33",
            startsAtLocal: new DateTime(2026, 11, 8, 11, 0, 0),
            hostUserId: sut.OtherHost.Id));

        var all = await sut.Service.GetDayAsync(new DateTime(2026, 11, 8));
        Assert.Equal(2, all.Count);

        sut.SignInAs(sut.Host);
        var scoped = await sut.Service.GetDayAsync(new DateTime(2026, 11, 8));

        Assert.Single(scoped);
        Assert.Equal("Ali Veli", scoped[0].PersonFullName);
    }

    [Fact]
    public async Task Search_MatchesNameAndPhoneWithoutDateFilter()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(sut.Input(startsAtLocal: new DateTime(2026, 12, 24, 15, 0, 0)));

        var byName = await sut.Service.SearchAsync("ali", null, null, null, null);
        var byPhone = await sut.Service.SearchAsync("532 123", null, null, null, null);
        var unrelated = await sut.Service.SearchAsync("mehmet", null, null, null, null);

        Assert.Single(byName);
        Assert.Single(byPhone);
        Assert.Empty(unrelated);
    }

    [Fact]
    public async Task Search_WithoutTermUsesRequestedDateRange()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(sut.Input(startsAtLocal: new DateTime(2026, 11, 8, 10, 0, 0)));

        var matchingDay = await sut.Service.SearchAsync(
            null, null, null, new DateTime(2026, 11, 8), new DateTime(2026, 11, 8));
        var otherDay = await sut.Service.SearchAsync(
            null, null, null, new DateTime(2026, 11, 9), new DateTime(2026, 11, 9));

        Assert.Single(matchingDay);
        Assert.Empty(otherDay);
    }

    [Fact]
    public async Task Update_ReplansPendingReminders()
    {
        using var sut = await CreateAsync();

        var created = await sut.Service.CreateAsync(sut.Input(offsets: new[] { 30 }));
        Assert.True(created.Success, created.Error);

        var updated = await sut.Service.UpdateAsync(
            created.AppointmentId!,
            sut.Input(startsAtLocal: new DateTime(2026, 11, 8, 12, 0, 0), offsets: new[] { 60, 30 }));
        Assert.True(updated.Success, updated.Error);

        using var db = sut.Db.CreateContext();
        var reminders = db.ReminderDispatches
            .Where(x => x.AppointmentId == created.AppointmentId
                        && x.Kind == ReminderKind.Appointment)
            .ToList();

        Assert.Single(reminders, x => x.State == ReminderState.Cancelled);
        var pending = reminders.Where(x => x.State == ReminderState.Pending).ToList();
        Assert.Equal(2, pending.Count);
        Assert.All(pending, x => Assert.Equal(
            new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc).AddMinutes(-x.OffsetMinutes),
            x.DueAtUtc));

        var detail = await sut.Service.GetDetailAsync(created.AppointmentId!);
        Assert.Equal(new[] { 60, 30 }, detail!.ReminderOffsetsMinutes);
        Assert.Contains(AuditAction.AppointmentUpdated, sut.Audit.Actions);
    }

    [Fact]
    public async Task Delete_RequiresAdminAndSoftDeletes()
    {
        using var sut = await CreateAsync();
        var created = await sut.Service.CreateAsync(sut.Input());

        var denied = await sut.Service.DeleteAsync(created.AppointmentId!);
        Assert.False(denied.Success);
        Assert.Contains("yönetici", denied.Error);

        sut.SignInAsRole(UserRole.Admin);
        var deleted = await sut.Service.DeleteAsync(created.AppointmentId!);

        Assert.True(deleted.Success, deleted.Error);
        Assert.Empty(await sut.Service.GetDayAsync(new DateTime(2026, 11, 8)));
        Assert.Contains(AuditAction.AppointmentDeleted, sut.Audit.Actions);
    }

    [Fact]
    public async Task Restore_RequiresAdmin()
    {
        using var sut = await CreateAsync();
        var created = await sut.Service.CreateAsync(sut.Input());
        sut.SignInAsRole(UserRole.Admin);
        await sut.Service.DeleteAsync(created.AppointmentId!);

        sut.SignInAsRole(UserRole.Secretary);
        var denied = await sut.Service.RestoreAsync(created.AppointmentId!);
        Assert.False(denied.Success);

        sut.SignInAsRole(UserRole.Admin);
        var restored = await sut.Service.RestoreAsync(created.AppointmentId!);
        Assert.True(restored.Success, restored.Error);
        Assert.Single(await sut.Service.GetDayAsync(new DateTime(2026, 11, 8)));
    }

    [Fact]
    public async Task Create_RejectsInactiveCategoryForNewAppointment()
    {
        using var sut = await CreateAsync();

        using (var db = sut.Db.CreateContext())
        {
            var category = db.Categories.Single(x => x.Id == sut.Category.Id);
            category.IsActive = false;
            db.SaveChanges();
        }

        var result = await sut.Service.CreateAsync(sut.Input());

        Assert.False(result.Success);
        Assert.Contains("Pasif kategori", result.Error);
    }

    [Fact]
    public async Task GetNextAppointment_ReturnsEarliestFutureOpenAppointment()
    {
        using var sut = await CreateAsync();

        // Yerel saat UTC+3; fixed now = 2026-11-06 12:00 yerel.
        await sut.Service.CreateAsync(sut.Input(
            firstName: "Geçmiş",
            phone: "0555 000 00 01",
            startsAtLocal: new DateTime(2026, 11, 6, 8, 0, 0)));
        await sut.Service.CreateAsync(sut.Input(
            firstName: "Yakın",
            phone: "0555 000 00 02",
            startsAtLocal: new DateTime(2026, 11, 6, 14, 0, 0)));
        await sut.Service.CreateAsync(sut.Input(
            firstName: "Uzak",
            phone: "0555 000 00 03",
            startsAtLocal: new DateTime(2026, 11, 7, 9, 0, 0)));

        var next = await sut.Service.GetNextAppointmentAsync();

        Assert.NotNull(next);
        Assert.Equal("Yakın Veli", next!.PersonFullName);
    }

    [Fact]
    public async Task GetNextAppointment_SkipsClosedAndScopesHost()
    {
        using var sut = await CreateAsync();

        var created = await sut.Service.CreateAsync(sut.Input(
            firstName: "Kapanan",
            phone: "0555 000 00 04",
            startsAtLocal: new DateTime(2026, 11, 6, 13, 0, 0)));
        await sut.Service.CreateAsync(sut.Input(
            firstName: "DiğerHost",
            phone: "0555 000 00 05",
            startsAtLocal: new DateTime(2026, 11, 6, 13, 30, 0),
            hostUserId: sut.OtherHost.Id));
        await sut.Service.CreateAsync(sut.Input(
            firstName: "KendiHost",
            phone: "0555 000 00 06",
            startsAtLocal: new DateTime(2026, 11, 6, 15, 0, 0),
            hostUserId: sut.Host.Id));

        using (var db = sut.Db.CreateContext())
        {
            var appointment = db.Appointments.Single(x => x.Id == created.AppointmentId);
            appointment.Cancel(new DateTime(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc));
            db.SaveChanges();
        }

        sut.SignInAs(sut.Host);

        var next = await sut.Service.GetNextAppointmentAsync();

        Assert.NotNull(next);
        Assert.Equal("KendiHost Veli", next!.PersonFullName);
    }

    [Fact]
    public async Task Search_FiltersByHost()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(sut.Input(startsAtLocal: new DateTime(2026, 11, 8, 10, 0, 0)));
        await sut.Service.CreateAsync(sut.Input(
            firstName: "Zeynep",
            phone: "0555 111 22 33",
            startsAtLocal: new DateTime(2026, 11, 8, 11, 0, 0),
            hostUserId: sut.OtherHost.Id));

        var filtered = await sut.Service.SearchAsync(
            null, null, null, new DateTime(2026, 11, 8), new DateTime(2026, 11, 8), sut.Host.Id);

        Assert.Single(filtered);
        Assert.Equal("Ali Veli", filtered[0].PersonFullName);
        Assert.Equal(sut.Host.Id, filtered[0].HostUserId);
    }

    [Fact]
    public async Task GetDeleted_ReturnsOnlyDeletedAppointmentsForAdmin()
    {
        using var sut = await CreateAsync();
        var created = await sut.Service.CreateAsync(sut.Input());

        sut.SignInAsRole(UserRole.Admin);
        await sut.Service.DeleteAsync(created.AppointmentId!);

        var deleted = await sut.Service.GetDeletedAsync();
        Assert.Single(deleted);
        Assert.Equal("Ali Veli", deleted[0].PersonFullName);
        Assert.InRange(deleted[0].RemainingDays, 0, 30);

        sut.SignInAsRole(UserRole.Secretary);
        Assert.Empty(await sut.Service.GetDeletedAsync());
    }

    [Fact]
    public async Task Update_ChangesCategoryAndListReflectsIt()
    {
        using var sut = await CreateAsync();

        Category otherCategory;
        using (var db = sut.Db.CreateContext())
        {
            otherCategory = new Category { Name = "Öğrenci Görüşmesi", ColorHex = "#7C3AED" };
            db.Categories.Add(otherCategory);
            db.SaveChanges();
        }

        var created = await sut.Service.CreateAsync(sut.Input());
        Assert.True(created.Success, created.Error);

        var updated = await sut.Service.UpdateAsync(
            created.AppointmentId!,
            sut.Input(categoryId: otherCategory.Id));
        Assert.True(updated.Success, updated.Error);

        var detail = await sut.Service.GetDetailAsync(created.AppointmentId!);
        Assert.Equal("Öğrenci Görüşmesi", detail!.CategoryName);

        var day = await sut.Service.GetDayAsync(new DateTime(2026, 11, 8));
        Assert.Equal("Öğrenci Görüşmesi", day.Single().CategoryName);
    }

    [Fact]
    public async Task Restore_ReplansCancelledFutureReminders()
    {
        using var sut = await CreateAsync();
        var created = await sut.Service.CreateAsync(sut.Input(offsets: new[] { 60, 30 }));

        sut.SignInAsRole(UserRole.Admin);
        await sut.Service.DeleteAsync(created.AppointmentId!);

        var restored = await sut.Service.RestoreAsync(created.AppointmentId!);

        Assert.True(restored.Success, restored.Error);
        Assert.Contains(AuditAction.AppointmentRestored, sut.Audit.Actions);
        Assert.Single(await sut.Service.GetDayAsync(new DateTime(2026, 11, 8)));

        using var db = sut.Db.CreateContext();
        var reminders = db.ReminderDispatches
            .Where(x => x.AppointmentId == created.AppointmentId)
            .ToList();

        Assert.Equal(3, reminders.Count);
        Assert.All(reminders, x => Assert.Equal(ReminderState.Pending, x.State));
        Assert.Single(reminders, x => x.Kind == ReminderKind.Call && x.OffsetMinutes == 5);
    }

    [Fact]
    public async Task Restore_ReactivatesOnlyLatestReminderPerOffset()
    {
        using var sut = await CreateAsync();
        var created = await sut.Service.CreateAsync(sut.Input(offsets: new[] { 30 }));

        var updated = await sut.Service.UpdateAsync(
            created.AppointmentId!,
            sut.Input(offsets: new[] { 60, 30 }));
        Assert.True(updated.Success, updated.Error);

        sut.SignInAsRole(UserRole.Admin);
        await sut.Service.DeleteAsync(created.AppointmentId!);

        var restored = await sut.Service.RestoreAsync(created.AppointmentId!);
        Assert.True(restored.Success, restored.Error);

        using var db = sut.Db.CreateContext();
        var reminders = db.ReminderDispatches
            .Where(x => x.AppointmentId == created.AppointmentId)
            .ToList();

        Assert.Equal(3, reminders.Count(x => x.State == ReminderState.Pending));
        Assert.Single(reminders,
            x => x.Kind == ReminderKind.Appointment && x.OffsetMinutes == 30 && x.State == ReminderState.Pending);
        Assert.Single(reminders,
            x => x.Kind == ReminderKind.Appointment && x.OffsetMinutes == 60 && x.State == ReminderState.Pending);
        Assert.Single(reminders,
            x => x.Kind == ReminderKind.Call && x.OffsetMinutes == 5 && x.State == ReminderState.Pending);
        Assert.Equal(2, reminders.Count(x => x.State == ReminderState.Cancelled));
    }

    [Fact]
    public async Task Restore_RejectsDeletionOlderThanThirtyDays()
    {
        using var sut = await CreateAsync();
        var created = await sut.Service.CreateAsync(sut.Input());

        sut.SignInAsRole(UserRole.Admin);
        await sut.Service.DeleteAsync(created.AppointmentId!);

        using (var db = sut.Db.CreateContext())
        {
            var appointment = db.Appointments.Single(x => x.Id == created.AppointmentId);
            appointment.DeletedAtUtc = NowUtc.AddDays(-31);
            db.SaveChanges();
        }

        var restored = await sut.Service.RestoreAsync(created.AppointmentId!);

        Assert.False(restored.Success);
        Assert.Contains("süresi doldu", restored.Error);
    }

    [Fact]
    public async Task PurgeExpiredDeleted_RemovesOnlyOlderThanThirtyDays()
    {
        using var sut = await CreateAsync();
        var old = await sut.Service.CreateAsync(sut.Input(offsets: new[] { 30 }));
        var recent = await sut.Service.CreateAsync(sut.Input(
            firstName: "Zeynep",
            phone: "0555 111 22 33",
            startsAtLocal: new DateTime(2026, 11, 8, 11, 0, 0)));

        sut.SignInAsRole(UserRole.Admin);
        await sut.Service.DeleteAsync(old.AppointmentId!);
        await sut.Service.DeleteAsync(recent.AppointmentId!);

        using (var db = sut.Db.CreateContext())
        {
            var appointment = db.Appointments.Single(x => x.Id == old.AppointmentId);
            appointment.DeletedAtUtc = NowUtc.AddDays(-31);
            db.SaveChanges();
        }

        var purged = await sut.Service.PurgeExpiredDeletedAsync(NowUtc);

        Assert.Equal(1, purged);
        Assert.Contains(AuditAction.AppointmentPurged, sut.Audit.Actions);
        Assert.Single(await sut.Service.GetDeletedAsync());

        using var verify = sut.Db.CreateContext();
        Assert.Empty(verify.ReminderDispatches
            .Where(x => x.AppointmentId == old.AppointmentId));
    }
}
