using System.Globalization;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Backups;

/// <summary>
/// Yedekteki verileri mevcut veritabanına bölüm bölüm birleştirir. Kullanıcılar
/// ve ayarlar yalnızca açıkça seçildiğinde yazılır; mevcut kayıtlar silinmez.
/// </summary>
public sealed class BackupRestoreEngine
{
    private readonly ISecretProtector _secrets;

    public BackupRestoreEngine(ISecretProtector secrets)
    {
        _secrets = secrets;
    }

    public async Task<RestoreReport> MergeAsync(
        AppDbContext live,
        string stagedDatabasePath,
        RestoreOptions options,
        string currentUserId,
        bool logoRestored,
        CancellationToken cancellationToken)
    {
        var selection = options.Selection.Normalize();
        var warnings = new List<string>();

        await using var source = CreateSourceContext(stagedDatabasePath);

        var report = new MutableReport();

        var users = selection.Has(BackupSection.Users)
            ? await MergeUsersAsync(live, source, report, warnings, cancellationToken)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        var categories = selection.Has(BackupSection.Categories)
            ? await MergeCategoriesAsync(live, source, report, warnings, cancellationToken)
            : await ExistingCategoryMapAsync(live, cancellationToken);

        var persons = selection.Has(BackupSection.Persons)
            ? await MergePersonsAsync(live, source, report, warnings, cancellationToken)
            : await ExistingPersonIdsAsync(live, cancellationToken);

        if (selection.Has(BackupSection.Appointments))
        {
            await MergeAppointmentsAsync(
                live,
                source,
                options.AppointmentMode,
                categories,
                persons,
                users,
                currentUserId,
                report,
                warnings,
                cancellationToken);
        }

        if (selection.Has(BackupSection.Settings))
            await MergeSettingsAsync(live, source, selection.SettingsGroups, options, report, warnings, cancellationToken);

        if (selection.Has(BackupSection.AuditLogs))
            await MergeAuditLogsAsync(live, source, report, cancellationToken);

        if (selection.Has(BackupSection.ShareLogs))
            await MergeShareLogsAsync(live, source, report, cancellationToken);

        if (selection.Has(BackupSection.ContentVersions))
            await MergeContentVersionsAsync(live, source, report, cancellationToken);

        if (selection.Has(BackupSection.Recipients))
            await MergeRecipientsAsync(live, source, report, cancellationToken);

        if (selection.Has(BackupSection.BackupHistory))
            await MergeBackupRecordsAsync(live, source, report, cancellationToken);

        await live.SaveChangesAsync(cancellationToken);

        if (await live.Users.CountAsync(cancellationToken) == 0)
            throw new InvalidOperationException(
                "Geri yükleme sonrası kullanıcı kalmadı; işlem geri alındı.");

        return report.Build(warnings, logoRestored);
    }

    private static AppDbContext CreateSourceContext(string path)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Mode=ReadOnly;Pooling=False")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Dictionary<string, string>> MergeUsersAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        List<string> warnings,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var liveById = await live.Users.ToDictionaryAsync(x => x.Id, StringComparer.Ordinal, ct);
        var liveByUsername = liveById.Values
            .ToDictionary(x => x.Username, StringComparer.OrdinalIgnoreCase);
        var liveByEmail = liveById.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var sourceUsers = await source.Users.AsNoTracking().ToListAsync(ct);

        foreach (var sourceUser in sourceUsers)
        {
            if (liveById.TryGetValue(sourceUser.Id, out var byId))
            {
                map[sourceUser.Id] = byId.Id;
                var id = byId.Id;
                live.Entry(byId).CurrentValues.SetValues(sourceUser);
                byId.Id = id;
                continue;
            }

            if (liveByUsername.TryGetValue(sourceUser.Username, out var sameName))
            {
                map[sourceUser.Id] = sameName.Id;
                report.UsersSkipped++;
                warnings.Add($"Kullanıcı adı zaten var, atlandı: {sourceUser.Username}");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sourceUser.Email)
                && liveByEmail.TryGetValue(sourceUser.Email, out var sameEmail))
            {
                map[sourceUser.Id] = sameEmail.Id;
                report.UsersSkipped++;
                warnings.Add($"E-posta adresi zaten var, atlandı: {sourceUser.Email}");
                continue;
            }

            live.Users.Add(sourceUser);
            liveById[sourceUser.Id] = sourceUser;
            liveByUsername[sourceUser.Username] = sourceUser;
            if (!string.IsNullOrWhiteSpace(sourceUser.Email))
                liveByEmail[sourceUser.Email] = sourceUser;
            map[sourceUser.Id] = sourceUser.Id;
            report.UsersAdded++;
        }

        await live.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, string>> MergeCategoriesAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        List<string> warnings,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var liveCategories = await live.Categories.ToListAsync(ct);
        var byId = liveCategories.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var byName = liveCategories.ToDictionary(
            x => x.Name,
            StringComparer.OrdinalIgnoreCase);

        var sourceCategories = await source.Categories.AsNoTracking().ToListAsync(ct);

        foreach (var sourceCategory in sourceCategories)
        {
            if (byId.TryGetValue(sourceCategory.Id, out var byIdMatch))
            {
                map[sourceCategory.Id] = byIdMatch.Id;
                var id = byIdMatch.Id;
                live.Entry(byIdMatch).CurrentValues.SetValues(sourceCategory);
                byIdMatch.Id = id;
                report.CategoriesUpdated++;
                continue;
            }

            if (byName.TryGetValue(sourceCategory.Name, out var byNameMatch))
            {
                map[sourceCategory.Id] = byNameMatch.Id;
                report.CategoriesMapped++;
                continue;
            }

            live.Categories.Add(sourceCategory);
            byId[sourceCategory.Id] = sourceCategory;
            byName[sourceCategory.Name] = sourceCategory;
            map[sourceCategory.Id] = sourceCategory.Id;
            report.CategoriesAdded++;
        }

        // Aynı isimli kategoriler birleştirildiği için randevu referansları korunur.
        if (report.CategoriesMapped > 0)
        {
            warnings.Add(
                $"{report.CategoriesMapped} kategori aynı adla mevcut kayıtla eşleştirildi.");
        }

        await live.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, string>> ExistingCategoryMapAsync(
        AppDbContext live,
        CancellationToken ct)
        => await live.Categories
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Id, StringComparer.Ordinal, ct);

    private static async Task<Dictionary<string, string>> MergePersonsAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        List<string> warnings,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var livePersons = await live.Persons.ToDictionaryAsync(x => x.Id, StringComparer.Ordinal, ct);

        var sourcePersons = await source.Persons.AsNoTracking().ToListAsync(ct);

        foreach (var sourcePerson in sourcePersons)
        {
            if (livePersons.TryGetValue(sourcePerson.Id, out var existing))
            {
                var id = existing.Id;
                live.Entry(existing).CurrentValues.SetValues(sourcePerson);
                existing.Id = id;
                report.PersonsUpdated++;
            }
            else
            {
                live.Persons.Add(sourcePerson);
                livePersons[sourcePerson.Id] = sourcePerson;
                report.PersonsAdded++;
            }

            map[sourcePerson.Id] = sourcePerson.Id;
        }

        if (report.PersonsUpdated > 0)
            warnings.Add($"{report.PersonsUpdated} kişi kaydı yedekteki bilgilerle güncellendi.");

        await live.SaveChangesAsync(ct);
        return map;
    }

    private static async Task<Dictionary<string, string>> ExistingPersonIdsAsync(
        AppDbContext live,
        CancellationToken ct)
        => await live.Persons
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Id, StringComparer.Ordinal, ct);

    private static async Task MergeAppointmentsAsync(
        AppDbContext live,
        AppDbContext source,
        AppointmentRestoreMode mode,
        Dictionary<string, string> categories,
        Dictionary<string, string> persons,
        Dictionary<string, string> users,
        string currentUserId,
        MutableReport report,
        List<string> warnings,
        CancellationToken ct)
    {
        if (mode == AppointmentRestoreMode.Replace)
        {
            live.ReminderDispatches.RemoveRange(await live.ReminderDispatches.ToListAsync(ct));
            live.AppointmentOutcomes.RemoveRange(await live.AppointmentOutcomes.ToListAsync(ct));
            live.Appointments.RemoveRange(await live.Appointments.ToListAsync(ct));
            await live.SaveChangesAsync(ct);
            warnings.Add("Mevcut randevular yedekteki kayıtlarla değiştirildi.");
        }

        var liveUsers = await live.Users.AsNoTracking().ToListAsync(ct);
        var liveUserIds = liveUsers.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var fallbackHost = liveUserIds.Contains(currentUserId)
            ? currentUserId
            : liveUsers.FirstOrDefault(x => x.Role == UserRole.Admin)?.Id
              ?? liveUsers.FirstOrDefault()?.Id
              ?? string.Empty;

        var liveAppointments = await live.Appointments
            .ToDictionaryAsync(x => x.Id, StringComparer.Ordinal, ct);

        var sourceAppointments = await source.Appointments.AsNoTracking().ToListAsync(ct);

        foreach (var sourceAppointment in sourceAppointments)
        {
            if (!persons.TryGetValue(sourceAppointment.PersonId, out var personId))
            {
                report.AppointmentsSkipped++;
                warnings.Add($"Kişisi bulunamadığı için randevu atlandı: {sourceAppointment.Id}");
                continue;
            }

            if (!categories.TryGetValue(sourceAppointment.CategoryId, out var categoryId))
            {
                report.AppointmentsSkipped++;
                warnings.Add($"Kategorisi bulunamadığı için randevu atlandı: {sourceAppointment.Id}");
                continue;
            }

            var hostId = sourceAppointment.HostUserId;
            if (users.TryGetValue(hostId, out var mappedUser))
            {
                hostId = mappedUser;
            }
            else if (!liveUserIds.Contains(hostId))
            {
                hostId = fallbackHost;
                if (!string.IsNullOrWhiteSpace(fallbackHost))
                    report.HostsMapped++;
            }

            if (liveAppointments.TryGetValue(sourceAppointment.Id, out var existing))
            {
                if (sourceAppointment.UpdatedAtUtc <= existing.UpdatedAtUtc)
                {
                    report.AppointmentsSkipped++;
                    continue;
                }

                live.Entry(existing).CurrentValues.SetValues(sourceAppointment);
                existing.PersonId = personId;
                existing.CategoryId = categoryId;
                existing.HostUserId = hostId;
                report.AppointmentsUpdated++;
            }
            else
            {
                sourceAppointment.PersonId = personId;
                sourceAppointment.CategoryId = categoryId;
                sourceAppointment.HostUserId = hostId;
                live.Appointments.Add(sourceAppointment);
                liveAppointments[sourceAppointment.Id] = sourceAppointment;
                report.AppointmentsAdded++;
            }
        }

        if (report.HostsMapped > 0)
        {
            warnings.Add(
                $"{report.HostsMapped} randevunun görüşmecisi mevcut kullanıcılarla eşleştirildi.");
        }

        await live.SaveChangesAsync(ct);

        await MergeOutcomesAsync(live, source, report, warnings, ct);
    }

    private static async Task MergeOutcomesAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        List<string> warnings,
        CancellationToken ct)
    {
        var appointmentIds = await live.Appointments
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(ct);
        var validIds = appointmentIds.ToHashSet(StringComparer.Ordinal);

        var liveOutcomes = await live.AppointmentOutcomes
            .ToDictionaryAsync(x => x.AppointmentId, StringComparer.Ordinal, ct);

        var sourceOutcomes = await source.AppointmentOutcomes.AsNoTracking().ToListAsync(ct);
        var restored = 0;

        foreach (var sourceOutcome in sourceOutcomes)
        {
            if (!validIds.Contains(sourceOutcome.AppointmentId))
                continue;

            if (liveOutcomes.TryGetValue(sourceOutcome.AppointmentId, out var existing))
            {
                if (sourceOutcome.UpdatedAtUtc > existing.UpdatedAtUtc)
                {
                    live.Entry(existing).CurrentValues.SetValues(sourceOutcome);
                    restored++;
                }
            }
            else
            {
                live.AppointmentOutcomes.Add(sourceOutcome);
                restored++;
            }
        }

        report.OutcomesRestored = restored;

        var sourceReminderCount = await source.ReminderDispatches.CountAsync(ct);
        if (sourceReminderCount > 0)
        {
            report.RemindersSkipped = sourceReminderCount;
            warnings.Add(
                $"{sourceReminderCount} hatırlatma kaydı taşınmadı; sunucu/yerel hatırlatıcı yeniden planlayacak.");
        }

        await live.SaveChangesAsync(ct);
    }

    private async Task MergeSettingsAsync(
        AppDbContext live,
        AppDbContext source,
        BackupSettingsGroup groups,
        RestoreOptions options,
        MutableReport report,
        List<string> warnings,
        CancellationToken ct)
    {
        if (groups == BackupSettingsGroup.None)
            return;

        var groupKeys = BackupSettingKeys.KeysFor(groups)
            .Where(x => !BackupSettingKeys.IsAlwaysExcluded(x))
            .ToList();

        var sourceSettings = await source.Settings
            .AsNoTracking()
            .ToListAsync(ct);

        sourceSettings = sourceSettings
            .Where(x => groupKeys.Contains(x.Key, StringComparer.Ordinal))
            .ToList();

        var skippedMachine = sourceSettings.Count(x =>
            BackupSettingKeys.IsMachineSpecific(x.Key) && !options.IncludeMachineSpecificSettings);

        sourceSettings = sourceSettings
            .Where(x => options.IncludeMachineSpecificSettings
                        || !BackupSettingKeys.IsMachineSpecific(x.Key))
            .ToList();

        var liveSettings = await live.Settings.ToDictionaryAsync(x => x.Key, StringComparer.Ordinal, ct);
        var skippedSecrets = 0;

        foreach (var sourceSetting in sourceSettings)
        {
            if (BackupSettingKeys.IsSecret(sourceSetting.Key)
                && !string.IsNullOrWhiteSpace(sourceSetting.Value)
                && !CanDecrypt(sourceSetting.Value))
            {
                skippedSecrets++;
                report.SettingsSkipped++;
                continue;
            }

            if (liveSettings.TryGetValue(sourceSetting.Key, out var existing))
            {
                existing.Value = sourceSetting.Value;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                live.Settings.Add(new Setting
                {
                    Key = sourceSetting.Key,
                    Value = sourceSetting.Value,
                    UpdatedAtUtc = DateTime.UtcNow,
                });
            }

            report.SettingsApplied++;
        }

        if (skippedSecrets > 0)
        {
            warnings.Add(
                $"{skippedSecrets} şifreli ayar bu Windows kullanıcısında çözülemediği için atlandı; ilgili parolaları yeniden girin.");
        }

        if (skippedMachine > 0)
        {
            warnings.Add(
                $"{skippedMachine} bu bilgisayara özel ayar korundu (yedek klasörü, otomatik başlatma vb.).");
        }

        await live.SaveChangesAsync(ct);
    }

    private bool CanDecrypt(string? value)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(_secrets.Unprotect(value));
        }
        catch
        {
            return false;
        }
    }

    private static async Task MergeAuditLogsAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        CancellationToken ct)
    {
        var existing = (await live.AuditLogs.AsNoTracking().Select(x => x.Id).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var rows = await source.AuditLogs.AsNoTracking().ToListAsync(ct);
        foreach (var row in rows)
        {
            if (!existing.Add(row.Id))
                continue;

            live.AuditLogs.Add(row);
            report.AuditLogsAdded++;
        }

        await live.SaveChangesAsync(ct);
    }

    private static async Task MergeShareLogsAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        CancellationToken ct)
    {
        var existing = (await live.WhatsAppShareLogs.AsNoTracking().Select(x => x.Id).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var rows = await source.WhatsAppShareLogs.AsNoTracking().ToListAsync(ct);
        foreach (var row in rows)
        {
            if (!existing.Add(row.Id))
                continue;

            live.WhatsAppShareLogs.Add(row);
            report.ShareLogsAdded++;
        }

        await live.SaveChangesAsync(ct);
    }

    private static async Task MergeContentVersionsAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        CancellationToken ct)
    {
        var existingIds = (await live.ContentVersions.AsNoTracking().Select(x => x.Id).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var existingNumbers = (await live.ContentVersions.AsNoTracking()
                .Select(x => x.VersionNumber)
                .ToListAsync(ct))
            .ToHashSet();

        var rows = await source.ContentVersions.AsNoTracking().ToListAsync(ct);
        foreach (var row in rows)
        {
            if (!existingIds.Add(row.Id) || !existingNumbers.Add(row.VersionNumber))
                continue;

            live.ContentVersions.Add(row);
            report.ContentVersionsAdded++;
        }

        await live.SaveChangesAsync(ct);
    }

    private static async Task MergeRecipientsAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        CancellationToken ct)
    {
        var existing = (await live.Recipients.AsNoTracking().Select(x => x.Id).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var rows = await source.Recipients.AsNoTracking().ToListAsync(ct);
        foreach (var row in rows)
        {
            if (!existing.Add(row.Id))
                continue;

            live.Recipients.Add(row);
            report.RecipientsAdded++;
        }

        await live.SaveChangesAsync(ct);
    }

    private static async Task MergeBackupRecordsAsync(
        AppDbContext live,
        AppDbContext source,
        MutableReport report,
        CancellationToken ct)
    {
        var existing = (await live.BackupRecords.AsNoTracking().Select(x => x.Id).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var rows = await source.BackupRecords.AsNoTracking().ToListAsync(ct);
        foreach (var row in rows)
        {
            if (!existing.Add(row.Id))
                continue;

            live.BackupRecords.Add(row);
            report.BackupRecordsAdded++;
        }

        await live.SaveChangesAsync(ct);
    }

    private sealed class MutableReport
    {
        public int PersonsAdded { get; set; }
        public int PersonsUpdated { get; set; }
        public int CategoriesAdded { get; set; }
        public int CategoriesUpdated { get; set; }
        public int CategoriesMapped { get; set; }
        public int AppointmentsAdded { get; set; }
        public int AppointmentsUpdated { get; set; }
        public int AppointmentsSkipped { get; set; }
        public int OutcomesRestored { get; set; }
        public int UsersAdded { get; set; }
        public int UsersSkipped { get; set; }
        public int SettingsApplied { get; set; }
        public int SettingsSkipped { get; set; }
        public int AuditLogsAdded { get; set; }
        public int ShareLogsAdded { get; set; }
        public int ContentVersionsAdded { get; set; }
        public int RecipientsAdded { get; set; }
        public int BackupRecordsAdded { get; set; }
        public int HostsMapped { get; set; }
        public int RemindersSkipped { get; set; }

        public RestoreReport Build(List<string> warnings, bool logoRestored)
            => new(
                PersonsAdded,
                PersonsUpdated,
                CategoriesAdded,
                CategoriesUpdated,
                CategoriesMapped,
                AppointmentsAdded,
                AppointmentsUpdated,
                AppointmentsSkipped,
                OutcomesRestored,
                UsersAdded,
                UsersSkipped,
                SettingsApplied,
                SettingsSkipped,
                AuditLogsAdded,
                ShareLogsAdded,
                ContentVersionsAdded,
                RecipientsAdded,
                BackupRecordsAdded,
                HostsMapped,
                RemindersSkipped,
                logoRestored,
                warnings);
    }
}
