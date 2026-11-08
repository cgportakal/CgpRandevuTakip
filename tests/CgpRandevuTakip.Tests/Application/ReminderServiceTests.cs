using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Reminders;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Application;

public class ReminderServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required RecordingAuditLogger Audit { get; init; }
        public required FixedClock Clock { get; init; }
        public required AppointmentService Appointments { get; init; }
        public required ReminderService Reminders { get; init; }
        public required User Host { get; init; }
        public required Category Category { get; init; }

        public void SignInAsRole(UserRole role)
            => CurrentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();
        var users = new UserRepository(db.Factory);
        var categories = new CategoryRepository(db.Factory);
        var persons = new PersonRepository(db.Factory);
        var appointments = new AppointmentRepository(db.Factory);

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
        var category = new Category { Name = "Veli Görüşmesi", ColorHex = "#2563EB" };

        await users.AddAsync(host);
        await categories.AddAsync(category);

        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("actor", "actor", "Aktör", UserRole.Secretary));
        var audit = new RecordingAuditLogger();
        var clock = new FixedClock(NowUtc);

        var appointmentService = new AppointmentService(
            appointments, persons, new UserRepository(db.Factory),
            new CategoryRepository(db.Factory), audit, clock, currentUser);

        var outcomeService = new AppointmentOutcomeService(
            appointments, new UserRepository(db.Factory), audit, clock, currentUser);

        var reminderService = new ReminderService(
            appointments, outcomeService, currentUser, clock, audit);

        return new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Audit = audit,
            Clock = clock,
            Appointments = appointmentService,
            Reminders = reminderService,
            Host = host,
            Category = category
        };
    }

    private static AppointmentInput Input(
        string? hostUserId,
        string? categoryId,
        DateTime startsAtLocal,
        IReadOnlyList<int> offsets)
        => new(
            "Ali", "Veli", "0532 123 45 67", startsAtLocal, 30,
            hostUserId!, categoryId!, AppointmentMethod.FaceToFace,
            null, null, null, offsets);

    [Fact]
    public async Task GetDue_ReturnsOnlyPendingDueActiveAppointments()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(Input(
            sut.Host.Id, sut.Category.Id,
            new DateTime(2026, 11, 6, 13, 0, 0), // yerel 13:00 = 10:00 UTC
            new[] { 30 }));
        Assert.True(created.Success, created.Error);

        // 30 dk hatırlatıcı 09:30 UTC; saat 09:31'e alınca due olur.
        sut.Clock.UtcNow = new DateTime(2026, 11, 6, 9, 31, 0, DateTimeKind.Utc);

        var due = await sut.Reminders.GetDueAsync(sut.Clock.UtcNow, 50);

        Assert.Single(due);
        Assert.Equal(ReminderKind.Appointment, due[0].Kind);
        Assert.Equal(30, due[0].OffsetMinutes);
        Assert.Equal("Ali Veli", due[0].PersonFullName);
        Assert.Equal("Host Bir", due[0].HostName);
    }

    [Fact]
    public async Task ApplyAction_DismissAndInformed_MarksStates()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(Input(
            sut.Host.Id, sut.Category.Id,
            new DateTime(2026, 11, 6, 13, 0, 0), new[] { 30 }));

        var reminderId = (await sut.Appointments.GetDetailAsync(created.AppointmentId!)) is null
            ? throw new InvalidOperationException()
            : await GetReminderIdAsync(sut, created.AppointmentId!, 30, ReminderKind.Appointment);

        var result = await sut.Reminders.ApplyActionAsync(
            reminderId, ReminderAction.InformedHost, sut.Clock.UtcNow);
        Assert.True(result.Success, result.Error);

        using var db = sut.Db.CreateContext();
        var reminder = db.ReminderDispatches.Single(x => x.Id == reminderId);
        Assert.Equal(ReminderState.InformedHost, reminder.State);
        Assert.Equal(ReminderAction.InformedHost, reminder.LastAction);
        Assert.NotNull(reminder.ActionAtUtc);
        Assert.Contains(AuditAction.ReminderActionTaken, sut.Audit.Actions);
    }

    [Fact]
    public async Task ApplyAction_Snooze_MovesDueTenMinutes()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(Input(
            sut.Host.Id, sut.Category.Id,
            new DateTime(2026, 11, 6, 13, 0, 0), new[] { 30 }));

        var reminderId = await GetReminderIdAsync(sut, created.AppointmentId!, 30, ReminderKind.Appointment);

        var now = new DateTime(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc);
        var result = await sut.Reminders.ApplyActionAsync(reminderId, ReminderAction.UnreachableSnooze, now);
        Assert.True(result.Success, result.Error);

        using var db = sut.Db.CreateContext();
        var reminder = db.ReminderDispatches.Single(x => x.Id == reminderId);
        Assert.Equal(ReminderState.Pending, reminder.State);
        Assert.Equal(now.AddMinutes(10), reminder.DueAtUtc);
        Assert.Equal(1, reminder.SnoozeCount);
        Assert.Equal(ReminderAction.UnreachableSnooze, reminder.LastAction);
    }

    [Fact]
    public async Task ApplyAction_VisitorCancelled_CreatesOutcomeAndCancels()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(Input(
            sut.Host.Id, sut.Category.Id,
            new DateTime(2026, 11, 6, 13, 0, 0), new[] { 30 }));

        var reminderId = await GetReminderIdAsync(sut, created.AppointmentId!, 30, ReminderKind.Appointment);

        var result = await sut.Reminders.ApplyActionAsync(
            reminderId, ReminderAction.VisitorCancelled, sut.Clock.UtcNow);
        Assert.True(result.Success, result.Error);

        using var db = sut.Db.CreateContext();
        var appointment = db.Appointments.Single(x => x.Id == created.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.True(appointment.IsClosed);

        var outcome = db.AppointmentOutcomes.Single(x => x.AppointmentId == created.AppointmentId);
        Assert.Equal(Attendance.Cancelled, outcome.Attendance);

        var reminder = db.ReminderDispatches.Single(x => x.Id == reminderId);
        Assert.Equal(ReminderState.Cancelled, reminder.State);
    }

    [Fact]
    public async Task RequeueExpired_RepeatsUpToMaxThenUnanswered()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(Input(
            sut.Host.Id, sut.Category.Id,
            new DateTime(2026, 11, 6, 13, 0, 0), new[] { 30 }));

        var reminderId = await GetReminderIdAsync(sut, created.AppointmentId!, 30, ReminderKind.Appointment);

        // İlk gönderim.
        await sut.Reminders.MarkWindowsNotifiedAsync(reminderId, NowUtc);
        await sut.Reminders.MarkSentAsync(reminderId);

        // 1. tekrar.
        sut.Clock.UtcNow = NowUtc.AddMinutes(11);
        var repeated = await sut.Reminders.RequeueExpiredAsync(sut.Clock.UtcNow, TimeSpan.FromMinutes(10), 2);
        Assert.Equal(1, repeated);

        await sut.Reminders.MarkWindowsNotifiedAsync(reminderId, sut.Clock.UtcNow);
        await sut.Reminders.MarkSentAsync(reminderId);

        // 2. tekrar.
        sut.Clock.UtcNow = NowUtc.AddMinutes(22);
        repeated = await sut.Reminders.RequeueExpiredAsync(sut.Clock.UtcNow, TimeSpan.FromMinutes(10), 2);
        Assert.Equal(1, repeated);

        await sut.Reminders.MarkWindowsNotifiedAsync(reminderId, sut.Clock.UtcNow);
        await sut.Reminders.MarkSentAsync(reminderId);

        // Sınır aşıldı → yanıtsız.
        sut.Clock.UtcNow = NowUtc.AddMinutes(33);
        repeated = await sut.Reminders.RequeueExpiredAsync(sut.Clock.UtcNow, TimeSpan.FromMinutes(10), 2);
        Assert.Equal(0, repeated);

        using var db = sut.Db.CreateContext();
        Assert.Equal(ReminderState.Unanswered, db.ReminderDispatches.Single(x => x.Id == reminderId).State);
    }

    private static async Task<string> GetReminderIdAsync(
        Sut sut,
        string appointmentId,
        int offsetMinutes,
        ReminderKind kind)
    {
        await using var db = sut.Db.CreateContext();
        var reminder = await db.ReminderDispatches
            .Where(x => x.AppointmentId == appointmentId
                        && x.Kind == kind
                        && x.OffsetMinutes == offsetMinutes)
            .FirstAsync();
        return reminder.Id;
    }
}
