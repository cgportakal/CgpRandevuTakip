using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Persistence;

public sealed record DatabaseSummary(
    string DatabasePath,
    int CategoryCount,
    int AppointmentCount,
    int UserCount,
    int SchemaVersion);

public static class DatabaseInitializer
{
    private static readonly (string Name, string Color)[] DefaultCategories =
    {
        ("Veli Görüşmesi", "#2E7D5B"),
        ("Öğrenci Görüşmesi", "#3E8E7E"),
        ("Rehberlik/Danışmanlık", "#5A8F3C"),
        ("Bilgi Talebi", "#2F7F86"),
        ("Şikâyet", "#B4433B"),
        ("Disiplin/İnceleme", "#9A6B1C"),
        ("Diğer", "#5D7166")
    };

    // 0.4.0 ve öncesinde oluşturulmuş varsayılan kategorilerin renklerini
    // yumuşak yeşil palete taşır. Kullanıcı özel rengi değiştirdiyse dokunulmaz.
    private static readonly (string Name, string LegacyColor, string Color)[] LegacyCategoryColors =
    {
        ("Veli Görüşmesi", "#2563EB", "#2E7D5B"),
        ("Öğrenci Görüşmesi", "#7C3AED", "#3E8E7E"),
        ("Rehberlik/Danışmanlık", "#059669", "#5A8F3C"),
        ("Bilgi Talebi", "#0891B2", "#2F7F86"),
        ("Şikâyet", "#DC2626", "#B4433B"),
        ("Disiplin/İnceleme", "#D97706", "#9A6B1C"),
        ("Diğer", "#64748B", "#5D7166")
    };

    public static DatabaseSummary Initialize(AppDbContext db, string databasePath)
    {
        db.Database.Migrate();

        if (!db.Categories.Any())
        {
            var order = 0;
            foreach (var (name, color) in DefaultCategories)
            {
                db.Categories.Add(new Category
                {
                    Name = name,
                    ColorHex = color,
                    SortOrder = order++,
                    IsActive = true
                });
            }

            db.SaveChanges();
        }

        NormalizeLegacyCategoryColors(db);

        return new DatabaseSummary(
            databasePath,
            db.Categories.Count(),
            db.Appointments.Count(),
            db.Users.Count(),
            AppConstants.SchemaVersion);
    }

    private static void NormalizeLegacyCategoryColors(AppDbContext db)
    {
        var categories = db.Categories.ToList();
        var changed = false;

        foreach (var (name, legacyColor, color) in LegacyCategoryColors)
        {
            var category = categories.FirstOrDefault(c =>
                c.Name == name &&
                string.Equals(c.ColorHex, legacyColor, StringComparison.OrdinalIgnoreCase));

            if (category is null)
                continue;

            category.ColorHex = color;
            changed = true;
        }

        if (changed)
            db.SaveChanges();
    }
}
