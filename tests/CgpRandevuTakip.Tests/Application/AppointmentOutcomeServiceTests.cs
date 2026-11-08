using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class AppointmentOutcomeServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required RecordingAuditLogger Audit { get; init; }
        public required AppointmentService Appointments { get; init; }
        public required AppointmentOutcomeService Outcomes { get; init; }
        public required User Host { get; init; }
        public required User OtherHost { get; init; }
        public required User Secretary { get; init; }
        public required Category Category { get; init; }

        public void SignInAs(User user)
            => CurrentUser.SignIn(new UserSession(user.Id, user.Username, user.DisplayName, user.Role));

        public AppointmentInput Input(
            DateTime? startsAtLocal = null,
            IReadOnlyList<int>? offsets = null,
            string? hostUserId = null)
            => new(
                "Ali",
                "Veli",
                "0532 123 45 67",
                startsAtLocal ?? new DateTime(2026, 11, 8, 10, 0, 0),
                30,
                hostUserId ?? Host.Id,
                Category.Id,
                AppointmentMethod.FaceToFace,
                null,
                null,
                null,
                offsets ?? Array.Empty<int>());

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

        var secretary = new User
        {
            Username = "secretary",
            DisplayName = "Sekreter",
            Role = UserRole.Secretary,
            IsActive = true,
            IsLoginEnabled = true,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };

        await users.AddAsync(host);
        await users.AddAsync(otherHost);
        await users.AddAsync(secretary);
        await categories.AddAsync(category);

        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession(
            secretary.Id,
            secretary.Username,
            secretary.DisplayName,
            secretary.Role));
        var audit = new RecordingAuditLogger();

        return new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Audit = audit,
            Appointments = new AppointmentService(
                new AppointmentRepository(db.Factory),
                new PersonRepository(db.Factory),
                new UserRepository(db.Factory),
                new CategoryRepository(db.Factory),
                audit,
                new FixedClock(NowUtc),
                currentUser),
            Outcomes = new AppointmentOutcomeService(
                new AppointmentRepository(db.Factory),
                new UserRepository(db.Factory),
                audit,
                new FixedClock(NowUtc),
                currentUser),
            Host = host,
            OtherHost = otherHost,
            Secretary = secretary,
            Category = category
        };
    }

    [Fact]
    public async Task Save_ArrivedAndClosed_CompletesAppointment()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(sut.Input(offsets: new[] { 60, 30 }));
        Assert.True(created.Success, created.Error);

        var result = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.Arrived, "Görüşme yapıldı", true, null, true));

        Assert.True(result.Success, result.Error);

        var detail = await sut.Appointments.GetDetailAsync(created.AppointmentId!);
        Assert.NotNull(detail);
        Assert.Equal(AppointmentStatus.Completed, detail!.Status);
        Assert.True(detail.IsClosed);
        Assert.NotNull(detail.Outcome);
        Assert.Equal(Attendance.Arrived, detail.Outcome!.Attendance);
        Assert.Equal("Görüşme yapıldı", detail.Outcome.Summary);
        Assert.Equal("Sekreter", detail.ClosedByName);
        Assert.Contains(AuditAction.OutcomeSaved, sut.Audit.Actions);

        using var db = sut.Db.CreateContext();
        var reminders = db.ReminderDispatches
            .Where(x => x.AppointmentId == created.AppointmentId)
            .ToList();
        Assert.All(reminders, x => Assert.Equal(ReminderState.Cancelled, x.State));
    }

    [Fact]
    public async Task Save_NoShowClosesAppointmentWithoutCompleting()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(sut.Input());
        Assert.True(created.Success, created.Error);

        var result = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.NoShow, null, false, null, true));

        Assert.True(result.Success, result.Error);

        var detail = await sut.Appointments.GetDetailAsync(created.AppointmentId!);
        Assert.Equal(AppointmentStatus.NoShow, detail!.Status);
        Assert.True(detail.IsClosed);
    }

    [Fact]
    public async Task Save_Postponed_KeepsAppointmentOpenAndStoresNote()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(sut.Input());
        Assert.True(created.Success, created.Error);

        var result = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.Postponed, null, true, "Yeni tarih verilecek", false));

        Assert.True(result.Success, result.Error);

        var detail = await sut.Appointments.GetDetailAsync(created.AppointmentId!);
        Assert.Equal(AppointmentStatus.Postponed, detail!.Status);
        Assert.False(detail.IsClosed);
        Assert.Equal("Yeni tarih verilecek", detail.Outcome!.PostNotes);
        Assert.True(detail.Outcome.WillRepeat);
    }

    [Fact]
    public async Task Save_ByOtherHost_IsDenied()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(sut.Input());
        Assert.True(created.Success, created.Error);

        sut.SignInAs(sut.OtherHost);

        var result = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.Arrived, null, false, null, true));

        Assert.False(result.Success);
        Assert.Contains("yetkiniz yok", result.Error);
    }

    [Fact]
    public async Task Save_KeepsOutcomeWhenEditedTwice()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(sut.Input());
        Assert.True(created.Success, created.Error);

        await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.NoShow, "İlk", false, null, true));

        var updated = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.Arrived, "İkinci", true, null, true));

        Assert.True(updated.Success, updated.Error);

        var outcome = await sut.Outcomes.GetAsync(created.AppointmentId!);
        Assert.NotNull(outcome);
        Assert.Equal(Attendance.Arrived, outcome!.Attendance);
        Assert.Equal("İkinci", outcome.Summary);
        Assert.True(outcome.IsClosed);

        using var db = sut.Db.CreateContext();
        Assert.Equal(1, db.AppointmentOutcomes.Count(x => x.AppointmentId == created.AppointmentId));
    }

    [Fact]
    public async Task Get_ReturnsNullForHostWhoCannotSeeAppointment()
    {
        using var sut = await CreateAsync();
        var created = await sut.Appointments.CreateAsync(sut.Input());
        Assert.True(created.Success, created.Error);

        await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.Arrived, null, false, null, true));

        sut.SignInAs(sut.OtherHost);

        Assert.Null(await sut.Outcomes.GetAsync(created.AppointmentId!));
    }
}
