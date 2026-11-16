using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Tests.Application;

public class AppointmentSeriesTests
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
        public required Category Category { get; init; }

        public void SignInAsRole(UserRole role)
            => CurrentUser.SignIn(new UserSession("actor", "actor", "Aktör", role));

        public AppointmentInput Input(DateTime startsAtLocal)
            => new(
                "Ali",
                "Veli",
                "0532 123 45 67",
                startsAtLocal,
                30,
                Host.Id,
                Category.Id,
                AppointmentMethod.FaceToFace,
                null,
                null,
                null,
                Array.Empty<int>());

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

        var category = new Category { Name = "Veli Görüşmesi", ColorHex = "#2563EB" };

        await users.AddAsync(host);
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
            Category = category
        };
    }

    [Fact]
    public async Task CreateSeries_Weekly_PersistsAllSlotsWithSeriesMetadata()
    {
        using var sut = await CreateAsync();

        var result = await sut.Service.CreateSeriesAsync(
            sut.Input(new DateTime(2026, 11, 10, 10, 0, 0)),
            new SeriesRequest(SeriesFrequency.Weekly, 4));

        Assert.True(result.Success, result.Error);
        Assert.Equal(4, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.NotNull(result.SeriesId);
        Assert.Contains(AuditAction.SeriesCreated, sut.Audit.Actions);

        using var db = sut.Db.CreateContext();
        var rows = db.Appointments
            .Where(x => x.SeriesId == result.SeriesId)
            .OrderBy(x => x.StartsAtUtc)
            .ToList();

        Assert.Equal(4, rows.Count);
        Assert.All(rows, x => Assert.Equal(4, x.SeriesCount));
        Assert.Equal(new int?[] { 1, 2, 3, 4 }, rows.Select(x => x.SeriesIndex));
        Assert.All(rows, x => Assert.Equal(AppointmentStatus.Scheduled, x.Status));
        Assert.Equal(
            new[]
            {
                new DateTime(2026, 11, 10, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 24, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 12, 1, 7, 0, 0, DateTimeKind.Utc)
            },
            rows.Select(x => x.StartsAtUtc));
    }

    [Fact]
    public async Task PreviewSeries_MarksConflictingSlotAndCreateSeriesSkipsIt()
    {
        using var sut = await CreateAsync();

        // İkinci haftanın saatinde başka bir görüşme var.
        var existing = await sut.Service.CreateAsync(
            sut.Input(new DateTime(2026, 11, 17, 10, 0, 0)));
        Assert.True(existing.Success, existing.Error);

        var request = new SeriesRequest(SeriesFrequency.Weekly, 4);
        var plan = await sut.Service.PreviewSeriesAsync(
            sut.Input(new DateTime(2026, 11, 10, 10, 0, 0)),
            request);

        Assert.Equal(4, plan.Slots.Count);
        Assert.Equal(3, plan.CreateCount);
        Assert.Equal(SeriesSlotState.Conflict, plan.Slots[1].State);
        Assert.True(plan.CanSave);

        var result = await sut.Service.CreateSeriesAsync(
            sut.Input(new DateTime(2026, 11, 10, 10, 0, 0)),
            request);

        Assert.True(result.Success, result.Error);
        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(3, result.Slots.Count(x => x.WillCreate));

        using var db = sut.Db.CreateContext();
        var rows = db.Appointments
            .Where(x => x.SeriesId == result.SeriesId)
            .OrderBy(x => x.SeriesIndex)
            .ToList();

        Assert.Equal(3, rows.Count);
        // Atlanan tarih seri numarasını tüketir; sıra numaraları korunur.
        Assert.Equal(new int?[] { 1, 3, 4 }, rows.Select(x => x.SeriesIndex));
    }

    [Fact]
    public async Task CreateSeries_FailsWhenNoSlotIsAvailable()
    {
        using var sut = await CreateAsync();

        await sut.Service.CreateAsync(
            sut.Input(new DateTime(2026, 11, 17, 10, 0, 0)));
        await sut.Service.CreateAsync(
            sut.Input(new DateTime(2026, 11, 24, 10, 0, 0)));

        var result = await sut.Service.CreateSeriesAsync(
            sut.Input(new DateTime(2026, 11, 17, 10, 0, 0)),
            new SeriesRequest(SeriesFrequency.Weekly, 2));

        Assert.False(result.Success);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(2, result.Slots.Count);
        Assert.DoesNotContain(AuditAction.SeriesCreated, sut.Audit.Actions);

        using var db = sut.Db.CreateContext();
        Assert.False(db.Appointments.Any(x => x.SeriesId != null));
    }

    [Fact]
    public async Task CancelSeries_CancelsOnlyFutureScheduledRows()
    {
        using var sut = await CreateAsync();

        var created = await sut.Service.CreateSeriesAsync(
            sut.Input(new DateTime(2026, 11, 10, 10, 0, 0)),
            new SeriesRequest(SeriesFrequency.Weekly, 3));
        Assert.True(created.Success, created.Error);

        // İkinci görüşme tamamlanmış, üçüncüsü geçmişte kalmış olsun.
        using (var db = sut.Db.CreateContext())
        {
            var second = db.Appointments.Single(
                x => x.SeriesId == created.SeriesId && x.SeriesIndex == 2);
            second.Status = AppointmentStatus.Completed;
            second.IsClosed = true;

            var third = db.Appointments.Single(
                x => x.SeriesId == created.SeriesId && x.SeriesIndex == 3);
            third.StartsAtUtc = NowUtc.AddDays(-1);

            await db.SaveChangesAsync();
        }

        var result = await sut.Service.CancelSeriesAsync(created.SeriesId!);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.CancelledCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Contains(AuditAction.SeriesCancelled, sut.Audit.Actions);

        using var verify = sut.Db.CreateContext();
        Assert.Equal(
            AppointmentStatus.Cancelled,
            verify.Appointments.Single(x => x.SeriesId == created.SeriesId && x.SeriesIndex == 1).Status);
        Assert.Equal(
            AppointmentStatus.Completed,
            verify.Appointments.Single(x => x.SeriesId == created.SeriesId && x.SeriesIndex == 2).Status);
        Assert.Equal(
            AppointmentStatus.Scheduled,
            verify.Appointments.Single(x => x.SeriesId == created.SeriesId && x.SeriesIndex == 3).Status);
    }

    [Fact]
    public async Task CancelSeries_FailsWhenNothingToCancel()
    {
        using var sut = await CreateAsync();

        var created = await sut.Service.CreateSeriesAsync(
            sut.Input(new DateTime(2026, 11, 10, 10, 0, 0)),
            new SeriesRequest(SeriesFrequency.Weekly, 2));
        Assert.True(created.Success, created.Error);

        using (var db = sut.Db.CreateContext())
        {
            foreach (var row in db.Appointments.Where(x => x.SeriesId == created.SeriesId))
                row.StartsAtUtc = NowUtc.AddDays(-2);
            await db.SaveChangesAsync();
        }

        var result = await sut.Service.CancelSeriesAsync(created.SeriesId!);

        Assert.False(result.Success);
        Assert.Equal(0, result.CancelledCount);
    }

    [Fact]
    public async Task Series_RequiresManagementRole()
    {
        using var sut = await CreateAsync();
        sut.SignInAsRole(UserRole.Host);

        var request = new SeriesRequest(SeriesFrequency.Weekly, 3);
        var input = sut.Input(new DateTime(2026, 11, 10, 10, 0, 0));

        var preview = await sut.Service.PreviewSeriesAsync(input, request);
        var created = await sut.Service.CreateSeriesAsync(input, request);
        var cancelled = await sut.Service.CancelSeriesAsync("any");

        Assert.NotNull(preview.Error);
        Assert.False(created.Success);
        Assert.False(cancelled.Success);
    }

    [Fact]
    public void SeriesRequest_ValidatesCountAndFrequency()
    {
        Assert.True(new SeriesRequest(SeriesFrequency.Monthly, 2).IsValid);
        Assert.True(new SeriesRequest(SeriesFrequency.Monthly, 12).IsValid);
        Assert.False(new SeriesRequest(SeriesFrequency.Weekly, 1).IsValid);
        Assert.False(new SeriesRequest(SeriesFrequency.Weekly, 13).IsValid);
        Assert.False(new SeriesRequest((SeriesFrequency)42, 4).IsValid);
    }
}
