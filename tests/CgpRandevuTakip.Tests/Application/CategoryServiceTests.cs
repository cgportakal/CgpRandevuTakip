using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Repositories;
using CgpRandevuTakip.Tests.Support;

namespace CgpRandevuTakip.Tests.Application;

public class CategoryServiceTests
{
    private static (CategoryService Service, CurrentUser CurrentUser, RecordingAuditLogger Audit, TestDatabase Db)
        CreateSut(UserRole role = UserRole.Admin)
    {
        var db = new TestDatabase();
        var currentUser = new CurrentUser();
        currentUser.SignIn(new UserSession("user-1", "test", "Test Kullanıcı", role));
        var audit = new RecordingAuditLogger();

        var service = new CategoryService(
            new CategoryRepository(db.Factory),
            audit,
            currentUser);

        return (service, currentUser, audit, db);
    }

    [Fact]
    public async Task Create_AddsCategoryAtEnd()
    {
        var (service, _, audit, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateAsync(new CategoryInput("Veli Görüşmesi", "#2e7d5b", true));

            Assert.True(result.Success, result.Error);
            var all = await service.GetAllAsync();
            Assert.Single(all);
            Assert.Equal("#2E7D5B", all[0].ColorHex);
            Assert.Equal(0, all[0].SortOrder);
            Assert.True(all[0].IsActive);
            Assert.Contains(AuditAction.CategoryCreated, audit.Actions);
        }
    }

    [Fact]
    public async Task Create_RejectsDuplicateNameIgnoringCase()
    {
        var (service, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateAsync(new CategoryInput("Veli Görüşmesi", "#2563EB", true));
            var duplicate = await service.CreateAsync(new CategoryInput("veli görüşmesi", "#7C3AED", true));

            Assert.False(duplicate.Success);
            Assert.Contains("zaten var", duplicate.Error);
            Assert.Single(await service.GetAllAsync());
        }
    }

    [Fact]
    public async Task Create_RejectsInvalidColor()
    {
        var (service, _, _, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateAsync(new CategoryInput("Kategori", "mavi", true));

            Assert.False(result.Success);
            Assert.Contains("#RRGGBB", result.Error);
        }
    }

    [Fact]
    public async Task Create_RejectsEmptyName()
    {
        var (service, _, _, db) = CreateSut();
        using (db)
        {
            var result = await service.CreateAsync(new CategoryInput("   ", "#2563EB", true));

            Assert.False(result.Success);
            Assert.Contains("zorunludur", result.Error);
        }
    }

    [Fact]
    public async Task Update_ChangesNameColorAndActiveState()
    {
        var (service, _, audit, db) = CreateSut();
        using (db)
        {
            await service.CreateAsync(new CategoryInput("Eski Ad", "#2563EB", true));
            var category = (await service.GetAllAsync())[0];

            var result = await service.UpdateAsync(
                category.Id,
                new CategoryInput("Yeni Ad", "#059669", false));

            Assert.True(result.Success, result.Error);
            var updated = (await service.GetAllAsync())[0];
            Assert.Equal("Yeni Ad", updated.Name);
            Assert.Equal("#059669", updated.ColorHex);
            Assert.False(updated.IsActive);
            Assert.Contains(AuditAction.CategoryUpdated, audit.Actions);
        }
    }

    [Fact]
    public async Task SetActive_TogglesVisibilityInActiveList()
    {
        var (service, _, audit, db) = CreateSut();
        using (db)
        {
            await service.CreateAsync(new CategoryInput("Kategori", "#2563EB", true));
            var id = (await service.GetAllAsync())[0].Id;

            var result = await service.SetActiveAsync(id, false);

            Assert.True(result.Success, result.Error);
            Assert.Empty(await service.GetActiveAsync());
            Assert.Single(await service.GetAllAsync());
            Assert.Contains(AuditAction.CategoryDeactivated, audit.Actions);
        }
    }

    [Fact]
    public async Task Delete_RemovesUnusedCategory()
    {
        var (service, _, audit, db) = CreateSut();
        using (db)
        {
            await service.CreateAsync(new CategoryInput("Silinecek", "#2563EB", true));
            var id = (await service.GetAllAsync())[0].Id;

            var result = await service.DeleteAsync(id);

            Assert.True(result.Success, result.Error);
            Assert.Empty(await service.GetAllAsync());
            Assert.Contains(AuditAction.CategoryDeleted, audit.Actions);
        }
    }

    [Fact]
    public async Task Delete_UsedCategory_ReturnsFailureAndKeepsRecord()
    {
        var (service, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateAsync(new CategoryInput("Kullanımda", "#2563EB", true));
            var categoryId = (await service.GetAllAsync())[0].Id;

            using (var context = db.CreateContext())
            {
                var user = new User
                {
                    Username = "host",
                    DisplayName = "Host",
                    PasswordHash = "hash",
                    PasswordSalt = "salt"
                };
                var person = new Person
                {
                    FirstName = "Örnek",
                    LastName = "Veli",
                    PhoneE164 = "+905321234567"
                };
                context.Users.Add(user);
                context.Persons.Add(person);
                context.Appointments.Add(new Appointment
                {
                    PersonId = person.Id,
                    HostUserId = user.Id,
                    CategoryId = categoryId,
                    StartsAtUtc = DateTime.UtcNow.AddHours(1),
                    Status = AppointmentStatus.Scheduled
                });
                context.SaveChanges();
            }

            var result = await service.DeleteAsync(categoryId);

            Assert.False(result.Success);
            Assert.Contains("kullanılıyor", result.Error);
            Assert.Single(await service.GetAllAsync());
        }
    }

    [Fact]
    public async Task Move_ReordersCategories()
    {
        var (service, _, _, db) = CreateSut();
        using (db)
        {
            await service.CreateAsync(new CategoryInput("A", "#2563EB", true));
            await service.CreateAsync(new CategoryInput("B", "#2563EB", true));
            await service.CreateAsync(new CategoryInput("C", "#2563EB", true));

            var c = (await service.GetAllAsync()).Single(x => x.Name == "C");
            var result = await service.MoveAsync(c.Id, -1);

            Assert.True(result.Success, result.Error);
            var names = (await service.GetAllAsync()).Select(x => x.Name).ToList();
            Assert.Equal(new[] { "A", "C", "B" }, names);
            Assert.Equal(new[] { 0, 1, 2 }, (await service.GetAllAsync()).Select(x => x.SortOrder).ToArray());
        }
    }

    [Fact]
    public async Task NonAdmin_CannotManageCategories()
    {
        var (service, _, _, db) = CreateSut(UserRole.Secretary);
        using (db)
        {
            var result = await service.CreateAsync(new CategoryInput("Yeni", "#2563EB", true));

            Assert.False(result.Success);
            Assert.Contains("yönetici", result.Error);
        }
    }
}
