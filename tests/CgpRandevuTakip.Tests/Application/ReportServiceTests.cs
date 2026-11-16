using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Reports;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class ReportServiceTests
{
    // FixedClock: 2026-11-06 09:00 UTC · yerel saat UTC+3.
    private static readonly DateTime NowUtc = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Sut : IDisposable
    {
        public required TestDatabase Db { get; init; }
        public required ReportService Service { get; init; }
        public required CurrentUser CurrentUser { get; init; }
        public required User HostA { get; init; }
        public required User HostB { get; init; }
        public required Category CategoryA { get; init; }
        public required Category CategoryB { get; init; }
        public required Person PersonOne { get; init; }
        public required Person PersonTwo { get; init; }

        public void SignInAs(UserRole role, string? userId = null)
            => CurrentUser.SignIn(new UserSession(
                userId ?? "actor",
                "actor",
                "Aktör",
                role));

        public void Dispose() => Db.Dispose();
    }

    private static async Task<Sut> CreateAsync()
    {
        var db = new TestDatabase();

        var hostA = new User
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

        var hostB = new User
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

        var categoryA = new Category { Name = "Veli Görüşmesi", ColorHex = "#2563EB" };
        var categoryB = new Category { Name = "Bilgi Talebi", ColorHex = "#B45309" };

        var personOne = new Person
        {
            FirstName = "Ali",
            LastName = "Veli",
            PhoneE164 = "+905321234567"
        };

        var personTwo = new Person
        {
            FirstName = "Ayşe",
            LastName = "Yılmaz",
            PhoneE164 = "+905329876543"
        };

        using (var context = db.CreateContext())
        {
            context.Users.AddRange(hostA, hostB);
            context.Categories.AddRange(categoryA, categoryB);
            context.Persons.AddRange(personOne, personTwo);
            await context.SaveChangesAsync();
        }

        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("actor", "actor", "Aktör", UserRole.Admin));

        var service = new ReportService(
            new ReportQueries(db.Factory),
            new FixedClock(NowUtc),
            currentUser);

        return new Sut
        {
            Db = db,
            Service = service,
            CurrentUser = currentUser,
            HostA = hostA,
            HostB = hostB,
            CategoryA = categoryA,
            CategoryB = categoryB,
            PersonOne = personOne,
            PersonTwo = personTwo
        };
    }

    private static async Task AddAppointmentAsync(
        Sut sut,
        User host,
        Category category,
        Person person,
        DateTime startsAtLocal,
        AppointmentStatus status = AppointmentStatus.Scheduled,
        bool isDeleted = false,
        int durationMinutes = 30,
        bool willRepeat = false)
    {
        using var db = sut.Db.CreateContext();
        var appointment = new Appointment
        {
            PersonId = person.Id,
            HostUserId = host.Id,
            CategoryId = category.Id,
            StartsAtUtc = startsAtLocal.AddHours(-3),
            DurationMinutes = durationMinutes,
            Status = status,
            IsClosed = status is AppointmentStatus.Completed or AppointmentStatus.Cancelled,
            IsDeleted = isDeleted,
            DeletedAtUtc = isDeleted ? NowUtc : null,
            UpdatedAtUtc = NowUtc
        };

        if (willRepeat)
        {
            appointment.Outcome = new AppointmentOutcome
            {
                AppointmentId = appointment.Id,
                Attendance = Attendance.Arrived,
                WillRepeat = true,
                UpdatedAtUtc = NowUtc
            };
        }

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
    }

    private static async Task<Sut> CreateSeededAsync()
    {
        var sut = await CreateAsync();

        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonOne,
            new DateTime(2026, 11, 2, 10, 0, 0), AppointmentStatus.Completed);
        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonOne,
            new DateTime(2026, 11, 2, 11, 0, 0), AppointmentStatus.NoShow);
        await AddAppointmentAsync(sut, sut.HostB, sut.CategoryB, sut.PersonTwo,
            new DateTime(2026, 11, 3, 9, 0, 0), AppointmentStatus.Arrived, willRepeat: true);
        await AddAppointmentAsync(sut, sut.HostB, sut.CategoryB, sut.PersonTwo,
            new DateTime(2026, 11, 4, 14, 0, 0), AppointmentStatus.Cancelled);
        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonTwo,
            new DateTime(2026, 11, 5, 15, 0, 0), AppointmentStatus.Scheduled, isDeleted: true);

        return sut;
    }

    [Fact]
    public async Task GetSummary_ComputesTotalsRatesAndBreakdowns()
    {
        using var sut = await CreateSeededAsync();

        var summary = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));

        Assert.Equal(4, summary.Total);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(1, summary.Arrived);
        Assert.Equal(1, summary.NoShow);
        Assert.Equal(1, summary.Cancelled);
        Assert.Equal(25, summary.CompletionRate, 3);
        Assert.Equal(25, summary.NoShowRate, 3);
        Assert.Equal(25, summary.CancelRate, 3);
        Assert.Equal(2, summary.UniquePersons);
        Assert.Equal(2, summary.ReturningPersons);
        Assert.Equal(1, summary.RepeatRequested);
        Assert.Equal(30, summary.AverageDurationMinutes, 3);
        Assert.Equal(120, summary.TotalMinutes);

        Assert.Equal(ReportGranularity.Daily, summary.Granularity);
        Assert.Equal(30, summary.Points.Count);
        Assert.Equal(2, summary.Points.Single(x => x.BucketStartLocal.Day == 2).Total);
        Assert.Equal(0, summary.Points.Single(x => x.BucketStartLocal.Day == 5).Total);

        Assert.Equal(2, summary.Categories.Count);
        Assert.Equal(2, summary.Categories.Single(x => x.CategoryId == sut.CategoryA.Id).Total);

        Assert.Equal(2, summary.Hosts.Count);
        Assert.Equal(2, summary.Hosts.Single(x => x.HostUserId == sut.HostA.Id).Total);
        Assert.Equal(1, summary.Hosts.Single(x => x.HostUserId == sut.HostB.Id).Cancelled);

        Assert.Equal(new[] { 9, 10, 11, 14 }, summary.Hours.Select(x => x.Hour));
    }

    [Fact]
    public async Task GetSummary_ScopesHostRoleToOwnAppointments()
    {
        using var sut = await CreateSeededAsync();
        sut.SignInAs(UserRole.Host, sut.HostB.Id);

        var summary = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));

        Assert.Equal(2, summary.Total);
        Assert.Single(summary.Hosts);
        Assert.Equal(sut.HostB.Id, summary.Hosts[0].HostUserId);
        Assert.Single(summary.Categories);
        Assert.Equal(sut.CategoryB.Id, summary.Categories[0].CategoryId);
    }

    [Fact]
    public async Task GetSummary_FiltersByCategoryAndHost()
    {
        using var sut = await CreateSeededAsync();

        var byCategory = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), sut.CategoryB.Id));
        Assert.Equal(2, byCategory.Total);

        var byHost = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), HostUserId: sut.HostA.Id));
        Assert.Equal(2, byHost.Total);
    }

    [Fact]
    public async Task GetSummary_UsesMonthlyBucketsForLongRanges()
    {
        using var sut = await CreateAsync();
        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonOne,
            new DateTime(2026, 1, 15, 10, 0, 0), AppointmentStatus.Completed);
        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonTwo,
            new DateTime(2026, 3, 10, 10, 0, 0), AppointmentStatus.Completed);

        var summary = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 1, 1), new DateTime(2026, 3, 31)));

        Assert.Equal(ReportGranularity.Monthly, summary.Granularity);
        Assert.Equal(3, summary.Points.Count);
        Assert.Equal(1, summary.Points[0].Total);
        Assert.Equal(0, summary.Points[1].Total);
        Assert.Equal(1, summary.Points[2].Total);
    }

    [Fact]
    public async Task GetSummary_RespectsLocalDayBoundaries()
    {
        using var sut = await CreateAsync();
        // Yerel 1 Kasım 00:00 (UTC 31 Ekim 21:00) dahil olmalı.
        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonOne,
            new DateTime(2026, 11, 1, 0, 0, 0));
        // Yerel 30 Kasım 23:30 dahil olmalı.
        await AddAppointmentAsync(sut, sut.HostA, sut.CategoryA, sut.PersonTwo,
            new DateTime(2026, 11, 30, 23, 30, 0));
        // Yerel 1 Aralık 00:00 hariç olmalı.
        await AddAppointmentAsync(sut, sut.HostB, sut.CategoryB, sut.PersonOne,
            new DateTime(2026, 12, 1, 0, 0, 0));

        var summary = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)));

        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Points[0].Total);
        Assert.Equal(1, summary.Points[29].Total);
    }

    [Fact]
    public async Task GetSummary_ReturnsEmptyForUnusedRange()
    {
        using var sut = await CreateAsync();

        var summary = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 5, 1), new DateTime(2026, 5, 31)));

        Assert.True(summary.IsEmpty);
        Assert.Equal(0, summary.CompletionRate);
        Assert.Empty(summary.Categories);
        Assert.Empty(summary.Hosts);
    }

    [Fact]
    public async Task GetSummary_SwapsReversedRange()
    {
        using var sut = await CreateSeededAsync();

        var summary = await sut.Service.GetSummaryAsync(
            new ReportQuery(new DateTime(2026, 11, 30), new DateTime(2026, 11, 1)));

        Assert.Equal(new DateTime(2026, 11, 1), summary.FromLocalDate);
        Assert.Equal(new DateTime(2026, 11, 30), summary.ToLocalDate);
        Assert.Equal(4, summary.Total);
    }
}
