using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Rooms;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class MeetingRoomsTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required AppointmentService Appointments { get; init; }
        public required AppointmentOutcomeService Outcomes { get; init; }
        public required MeetingRoomService Rooms { get; init; }
        public required User HostA { get; init; }
        public required User HostB { get; init; }
        public required User HostC { get; init; }
        public required Category Category { get; init; }

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();
        var users = new UserRepository(db.Factory);
        var categories = new CategoryRepository(db.Factory);
        var appointments = new AppointmentRepository(db.Factory);
        var persons = new PersonRepository(db.Factory);
        var roomsRepo = new MeetingRoomRepository(db.Factory);
        var audit = new RecordingAuditLogger();
        var clock = new FixedClock(NowUtc);
        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("secretary", "secretary", "Sekreter", UserRole.Secretary));

        var hostA = new User
        {
            Username = "host-a",
            DisplayName = "Ayşe Yılmaz",
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = false,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };
        var hostB = new User
        {
            Username = "host-b",
            DisplayName = "Mehmet Kaya",
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = false,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };
        var hostC = new User
        {
            Username = "host-c",
            DisplayName = "Zeynep Demir",
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = false,
            PasswordHash = "!",
            PasswordSalt = "!",
            PasswordIterations = 1
        };
        var category = new Category { Name = "Veli Görüşmesi", ColorHex = "#2E7D5B" };

        await users.AddAsync(hostA);
        await users.AddAsync(hostB);
        await users.AddAsync(hostC);
        await categories.AddAsync(category);

        var rooms = new MeetingRoomService(roomsRepo, users, audit, currentUser, clock);
        var appointmentsService = new AppointmentService(
            appointments,
            persons,
            users,
            categories,
            rooms,
            audit,
            clock,
            currentUser);

        var outcomes = new AppointmentOutcomeService(
            appointments,
            users,
            audit,
            clock,
            currentUser);

        return new Sut
        {
            Db = db,
            CurrentUser = currentUser,
            Appointments = appointmentsService,
            Outcomes = outcomes,
            Rooms = rooms,
            HostA = hostA,
            HostB = hostB,
            HostC = hostC,
            Category = category
        };
    }

    private static AppointmentInput Input(
        Sut sut,
        string hostId,
        DateTime startsAtLocal,
        IReadOnlyList<AppointmentGuestInput>? guests = null,
        IReadOnlyList<string>? participants = null,
        string? roomId = null,
        string firstName = "Ali",
        string lastName = "Veli",
        string phone = "0532 111 22 33")
        => new(
            firstName,
            lastName,
            phone,
            startsAtLocal,
            30,
            hostId,
            sut.Category.Id,
            AppointmentMethod.FaceToFace,
            null,
            null,
            null,
            new[] { 30 },
            RoomId: roomId,
            ParticipantHostUserIds: participants,
            Guests: guests);

    [Fact]
    public async Task Create_WithGuestsAndParticipants_CreatesRelations()
    {
        using var sut = await CreateAsync();

        var guests = new[]
        {
            new AppointmentGuestInput(null, "Ali", "Veli", "0532 111 22 33", IsPrimary: true),
            new AppointmentGuestInput(null, "Ayşe", "Veli", "0532 111 22 44"),
            new AppointmentGuestInput(null, "Mehmet", "Veli", "0532 111 22 55")
        };

        var result = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostA.Id,
            new DateTime(2026, 11, 9, 10, 0, 0),
            guests,
            new[] { sut.HostB.Id, sut.HostC.Id }));

        Assert.True(result.Success, result.Error);
        var detail = await sut.Appointments.GetDetailAsync(result.AppointmentId!);

        Assert.NotNull(detail);
        Assert.Equal(3, detail!.Guests!.Count);
        Assert.Equal(3, detail.Participants!.Count);
        Assert.Equal("Ayşe Yılmaz Odası", detail.RoomName);
        Assert.Equal("Ali Veli", detail.Guests!.Single(x => x.IsPrimary).FullName);
        Assert.All(detail.Participants!, x => Assert.False(string.IsNullOrWhiteSpace(x.DisplayName)));
    }

    [Fact]
    public async Task RoomConflict_IsReported()
    {
        using var sut = await CreateAsync();
        await sut.Rooms.EnsureHostRoomAsync(sut.HostA.Id, sut.HostA.DisplayName);
        var room = await sut.Rooms.GetHostRoomAsync(sut.HostA.Id);

        var first = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostA.Id,
            new DateTime(2026, 11, 9, 10, 0, 0),
            roomId: room!.Id));
        Assert.True(first.Success, first.Error);

        var second = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostB.Id,
            new DateTime(2026, 11, 9, 10, 15, 0),
            participants: new[] { sut.HostA.Id },
            roomId: room.Id,
            firstName: "Başka",
            phone: "0532 999 88 77"));

        Assert.False(second.Success);
        Assert.Contains(second.Conflicts!, x => x.Kind == ConflictKind.Room);
        Assert.Contains(second.Conflicts!, x => x.Kind == ConflictKind.Host);
    }

    [Fact]
    public async Task GuestConflict_IsReported()
    {
        using var sut = await CreateAsync();

        var first = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostA.Id,
            new DateTime(2026, 11, 9, 11, 0, 0),
            new[]
            {
                new AppointmentGuestInput(null, "Ali", "Veli", "0532 111 22 33", IsPrimary: true)
            }));
        Assert.True(first.Success, first.Error);

        var second = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostB.Id,
            new DateTime(2026, 11, 9, 11, 15, 0),
            new[]
            {
                new AppointmentGuestInput(null, "Ali", "Veli", "0532 111 22 33", IsPrimary: true)
            },
            firstName: "Ali",
            phone: "0532 111 22 33"));

        Assert.False(second.Success);
        Assert.Contains(second.Conflicts!, x => x.Kind == ConflictKind.Guest);
    }

    [Fact]
    public async Task SaveOutcome_PerGuest_AggregatesStatus()
    {
        using var sut = await CreateAsync();

        var created = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostA.Id,
            new DateTime(2026, 11, 9, 12, 0, 0),
            new[]
            {
                new AppointmentGuestInput(null, "Ali", "Veli", "0532 111 22 33", IsPrimary: true),
                new AppointmentGuestInput(null, "Ayşe", "Veli", "0532 111 22 44")
            }));
        Assert.True(created.Success, created.Error);

        var detail = await sut.Appointments.GetDetailAsync(created.AppointmentId!);
        var primary = detail!.Guests!.Single(x => x.IsPrimary);
        var secondary = detail.Guests!.Single(x => !x.IsPrimary);

        var result = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(
                Attendance.Arrived,
                "Genel değerlendirme",
                false,
                null,
                IsClosed: true,
                new[]
                {
                    new GuestOutcomeInput(primary.GuestId, Attendance.Arrived, "Geldi", false, null, true),
                    new GuestOutcomeInput(secondary.GuestId, Attendance.NoShow, "Gelmedi", false, null, true)
                }));

        Assert.True(result.Success, result.Error);

        var updated = await sut.Appointments.GetDetailAsync(created.AppointmentId!);
        Assert.Equal(AppointmentStatus.Completed, updated!.Status);
        Assert.Equal(Attendance.Arrived, updated.Outcome!.Attendance);
        Assert.Equal(2, updated.Outcome.Guests!.Count);
        Assert.Contains(updated.Outcome.Guests!, x => x.Outcome?.Attendance == Attendance.NoShow);
    }

    [Fact]
    public async Task ParticipantHost_CanReadAndSaveOutcome()
    {
        using var sut = await CreateAsync();

        var created = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostA.Id,
            new DateTime(2026, 11, 9, 13, 0, 0),
            participants: new[] { sut.HostB.Id }));
        Assert.True(created.Success, created.Error);

        sut.CurrentUser.SignIn(new UserSession(
            sut.HostB.Id,
            sut.HostB.Username,
            sut.HostB.DisplayName,
            UserRole.Host));

        var detail = await sut.Appointments.GetDetailAsync(created.AppointmentId!);
        Assert.NotNull(detail);

        var saved = await sut.Outcomes.SaveAsync(
            created.AppointmentId!,
            new AppointmentOutcomeInput(Attendance.NoShow, null, false, null, true));

        Assert.True(saved.Success, saved.Error);
    }

    [Fact]
    public async Task LegacyInput_StillCreatesSingleGuestAndHost()
    {
        using var sut = await CreateAsync();

        var result = await sut.Appointments.CreateAsync(Input(
            sut,
            sut.HostA.Id,
            new DateTime(2026, 11, 9, 14, 0, 0)));

        Assert.True(result.Success, result.Error);
        var detail = await sut.Appointments.GetDetailAsync(result.AppointmentId!);

        Assert.NotNull(detail);
        Assert.Single(detail!.Guests!);
        Assert.Single(detail.Participants!);
        Assert.True(detail.Guests!.Single().IsPrimary);
        Assert.True(detail.Participants!.Single().IsPrimary);
    }
}
