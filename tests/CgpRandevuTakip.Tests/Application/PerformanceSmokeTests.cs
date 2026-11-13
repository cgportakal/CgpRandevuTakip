using System.Diagnostics;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;
using Xunit.Abstractions;

namespace CgpRandevuTakip.Tests.Application;

/// <summary>
/// NFR-PERF-02 için kaba performans kontrolü. Eşikler CI makinesindeki
/// dalgalanmalara karşı hedefin üzerinde tutulur.
/// </summary>
public class PerformanceSmokeTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task QueryAsync_ThousandAppointmentsInOneDay_UnderThreeSeconds()
    {
        using var db = new TestDatabase();

        var person = new Person
        {
            FirstName = "Performans",
            LastName = "Testi",
            PhoneE164 = "+905550000000"
        };
        var host = new User
        {
            Username = "perf.host",
            DisplayName = "Performans Görüşmeci",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            PasswordIterations = 1,
            Role = UserRole.Host,
            IsActive = true,
            IsLoginEnabled = false
        };
        var category = new Category
        {
            Name = "Performans",
            ColorHex = "#2E7D5B",
            SortOrder = 1,
            IsActive = true
        };

        using (var context = db.CreateContext())
        {
            context.Persons.Add(person);
            context.Users.Add(host);
            context.Categories.Add(category);
            await context.SaveChangesAsync();
        }

        var dayStart = new DateTime(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
        using (var context = db.CreateContext())
        {
            for (var i = 0; i < 1000; i++)
            {
                context.Appointments.Add(new Appointment
                {
                    PersonId = person.Id,
                    HostUserId = host.Id,
                    CategoryId = category.Id,
                    StartsAtUtc = dayStart.AddMinutes(i),
                    DurationMinutes = 30,
                    Method = AppointmentMethod.FaceToFace,
                    Status = i % 5 == 0 ? AppointmentStatus.Completed : AppointmentStatus.Scheduled,
                    CreatedByUserId = host.Id
                });
            }

            await context.SaveChangesAsync();
        }

        var repository = new AppointmentRepository(db.Factory);
        var stopwatch = Stopwatch.StartNew();

        var rows = await repository.QueryAsync(new AppointmentQuery(
            dayStart,
            dayStart.AddDays(1),
            null,
            null,
            null,
            null));

        stopwatch.Stop();
        _output.WriteLine($"1000 kayıt sorgusu: {stopwatch.Elapsed.TotalMilliseconds:N0} ms");

        Assert.Equal(1000, rows.Count);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"Sorgu beklenenden yavaş: {stopwatch.Elapsed.TotalMilliseconds:N0} ms");
    }
}
