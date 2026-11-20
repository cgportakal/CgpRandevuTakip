using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CgpRandevuTakip.Infrastructure.Backups;

/// <summary>
/// Seçilebilir bölümlerden ZIP yedeği üretir, yedeği doğrular/inceler ve
/// geri yüklemeyi mevcut veritabanına bölüm bölüm birleştirir.
/// Yedekler varsayılan olarak son 30 dosyayla sınırlıdır.
/// </summary>
public sealed class BackupService : IBackupService
{
    private const int RetentionCount = 30;
    private const string DatabaseEntry = "data.db";
    private const string SettingsEntry = "settings.json";
    private const string ManifestEntry = "manifest.json";
    private const string LogoEntryPrefix = "branding/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ISettingsService _settings;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAppEnvironment _environment;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IGoogleDriveBackupUploader _driveUploader;
    private readonly ISecretProtector _secrets;

    public BackupService(
        IDbContextFactory<AppDbContext> dbFactory,
        ISettingsService settings,
        ISettingsRepository settingsRepository,
        IAppEnvironment environment,
        IAuditLogger audit,
        ICurrentUser currentUser,
        IClock clock,
        IGoogleDriveBackupUploader driveUploader,
        ISecretProtector secrets)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _settingsRepository = settingsRepository;
        _environment = environment;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
        _driveUploader = driveUploader;
        _secrets = secrets;
    }

    public async Task<BackupCreateResult> CreateAsync(
        BackupTrigger trigger,
        BackupOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var selection = (options ?? BackupOptions.Full).Selection.Normalize();

        var tempDb = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            $"backup-{Guid.NewGuid():N}.db");
        var tempZip = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            $"backup-{Guid.NewGuid():N}.zip");

        string? partialPath = null;
        try
        {
            var settings = await _settings.GetAsync();
            var directory = string.IsNullOrWhiteSpace(settings.BackupDirectory)
                ? _environment.DefaultBackupDirectory
                : settings.BackupDirectory;

            var encrypted = settings.BackupEncryptionEnabled;
            if (encrypted && string.IsNullOrWhiteSpace(settings.BackupPassword))
                return BackupCreateResult.Fail("Yedek şifreleme açık ancak yedek parolası tanımlı değil.");

            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.GetDirectoryName(tempDb)!);

            var localNow = _clock.ToLocal(_clock.UtcNow);
            var path = BuildUniquePath(directory, localNow, encrypted);
            partialPath = path;

            // SQLite'ın çevrimiçi yedek API'si: uygulama açıkken tutarlı kopya alır.
            await using (var source = new SqliteConnection($"Data Source={_environment.DatabasePath};Mode=ReadOnly;Pooling=False"))
            await using (var destination = new SqliteConnection($"Data Source={tempDb};Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            ApplySelection(tempDb, selection);

            var logoPath = selection.Has(BackupSection.Logo) ? FindLogo() : null;
            var summary = await BackupContentInspector.ReadAsync(
                tempDb,
                logoPath is not null,
                cancellationToken);

            var databaseHash = await ComputeFileHashAsync(tempDb, cancellationToken);
            var settingsJson = await BuildSettingsJsonAsync(selection, cancellationToken);
            var settingsHash = ComputeTextHash(settingsJson);

            var manifest = new BackupManifest(
                AppConstants.ProductName,
                AppConstants.Version,
                AppConstants.SchemaVersion,
                _clock.UtcNow,
                summary.UserCount,
                summary.PersonCount,
                summary.AppointmentCount,
                databaseHash,
                settingsHash)
            {
                ManifestVersion = 2,
                Selection = selection,
                Summary = summary,
            };

            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);

            using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tempDb, DatabaseEntry, CompressionLevel.Optimal);
                AddTextEntry(zip, SettingsEntry, settingsJson);
                AddTextEntry(zip, ManifestEntry, manifestJson);

                if (logoPath is not null)
                    zip.CreateEntryFromFile(logoPath, LogoEntryPrefix + Path.GetFileName(logoPath), CompressionLevel.Optimal);
            }

            if (encrypted)
                BackupCipher.EncryptFile(tempZip, path, settings.BackupPassword);
            else
                File.Copy(tempZip, path, overwrite: true);

            var fileInfo = new FileInfo(path);
            var record = new BackupRecord
            {
                FileName = Path.GetFileName(path),
                Path = path,
                Provider = BackupProvider.Local,
                SizeBytes = fileInfo.Length,
                Sha256 = databaseHash,
                SchemaVersion = AppConstants.SchemaVersion,
                IsVerified = true,
                CreatedAtUtc = _clock.UtcNow
            };

            await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
            {
                db.BackupRecords.Add(record);
                await db.SaveChangesAsync(cancellationToken);
            }

            await ApplyRetentionAsync(directory);

            await _audit.LogAsync(
                AuditAction.BackupCreated,
                _currentUser.UserId,
                nameof(BackupRecord),
                record.Id,
                JsonSerializer.Serialize(new
                {
                    trigger,
                    record.FileName,
                    sections = selection.Sections.ToString(),
                    settingsGroups = selection.SettingsGroups.ToString(),
                    summary.UserCount,
                    summary.AppointmentCount,
                }));

            // Google Drive yüklemesi yerel yedeği bozmaz; hata olsa da yerel yedek geçerlidir.
            try
            {
                await _driveUploader.UploadAsync(path, cancellationToken);
            }
            catch
            {
                // Yükleme hatası sonraki denemede ele alınır.
            }

            return BackupCreateResult.Ok(ToInfo(record));
        }
        catch (Exception ex)
        {
            if (partialPath is not null)
                TryDelete(partialPath);

            return BackupCreateResult.Fail($"Yedek oluşturulamadı: {ex.Message}");
        }
        finally
        {
            TryDelete(tempDb);
            TryDelete(tempZip);
        }
    }

    public async Task<IReadOnlyList<BackupInfo>> GetHistoryAsync(
        int count = 50,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.BackupRecords
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(count, 1, 500))
            .ToListAsync(cancellationToken);

        var result = records
            .Where(x => File.Exists(x.Path))
            .Select(ToInfo)
            .ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);

        // Veritabanı kaydı olmayan (ör. geri yükleme öncesi kopyalanmış) dosyalar da listede görünür.
        var settings = await _settings.GetAsync();
        var directory = string.IsNullOrWhiteSpace(settings.BackupDirectory)
            ? _environment.DefaultBackupDirectory
            : settings.BackupDirectory;

        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory, BackupFileNameBuilder.Prefix + "*"))
            {
                if (!BackupFileNameBuilder.IsValid(Path.GetFileName(file)))
                    continue;

                if (result.ContainsKey(file))
                    continue;

                var created = ParseTimestamp(Path.GetFileName(file)) ?? File.GetLastWriteTime(file);
                result[file] = new BackupInfo(
                    string.Empty,
                    Path.GetFileName(file),
                    file,
                    created,
                    new FileInfo(file).Length,
                    await QuickVerifyAsync(file, settings.BackupPassword),
                    string.Empty);
            }
        }

        return result.Values
            .OrderByDescending(x => x.CreatedAtLocal)
            .Take(Math.Clamp(count, 1, 500))
            .ToList();
    }

    /// <summary>Liste için hızlı doğrulama: manifest + veritabanı özeti (çıkarma yapmaz).</summary>
    private static async Task<bool> QuickVerifyAsync(string path, string? password)
    {
        var readablePath = path;
        string? tempPath = null;

        try
        {
            if (BackupFileNameBuilder.IsEncrypted(Path.GetFileName(path))
                || BackupCipher.IsEncryptedFile(path))
            {
                if (string.IsNullOrWhiteSpace(password))
                    return false;

                tempPath = Path.Combine(
                    Path.GetTempPath(),
                    "CgpRandevuTakip",
                    $"verify-{Guid.NewGuid():N}.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
                BackupCipher.DecryptFile(path, tempPath, password);
                readablePath = tempPath;
            }

            await Task.CompletedTask;
            using var zip = ZipFile.OpenRead(readablePath);
            var manifestEntry = zip.GetEntry(ManifestEntry);
            var databaseEntry = zip.GetEntry(DatabaseEntry);
            if (manifestEntry is null || databaseEntry is null)
                return false;

            BackupManifest? manifest;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
                manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd(), JsonOptions);

            if (manifest is null || manifest.SchemaVersion != AppConstants.SchemaVersion)
                return false;

            using var stream = databaseEntry.Open();
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(stream));
            return string.Equals(hash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static DateTime? ParseTimestamp(string fileName)
    {
        if (!BackupFileNameBuilder.IsValid(fileName))
            return null;

        var extensionLength = BackupFileNameBuilder.IsEncrypted(fileName)
            ? BackupFileNameBuilder.EncryptedExtension.Length
            : BackupFileNameBuilder.Extension.Length;

        var stamp = fileName[BackupFileNameBuilder.Prefix.Length..^extensionLength];
        return DateTime.TryParseExact(
            stamp,
            "yyyy-MM-dd_HH-mm-ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    public async Task<BackupValidationResult> ValidateAsync(
        string zipPath,
        CancellationToken cancellationToken = default)
    {
        var stagedPath = string.Empty;
        string? decryptedZip = null;
        try
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                return BackupValidationResult.Fail("Yedek dosyası bulunamadı.");

            var prepared = await PrepareZipAsync(zipPath);
            if (prepared.Error is not null)
                return BackupValidationResult.Fail(prepared.Error);

            decryptedZip = prepared.TempPath;
            using var zip = ZipFile.OpenRead(prepared.Path);
            var manifestEntry = zip.GetEntry(ManifestEntry);
            var databaseEntry = zip.GetEntry(DatabaseEntry);
            var settingsEntry = zip.GetEntry(SettingsEntry);

            if (manifestEntry is null || databaseEntry is null)
                return BackupValidationResult.Fail("Yedek içeriği eksik: data.db veya manifest.json yok.");

            BackupManifest? manifest;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            {
                manifest = JsonSerializer.Deserialize<BackupManifest>(
                    await reader.ReadToEndAsync(cancellationToken),
                    JsonOptions);
            }

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.DatabaseSha256))
                return BackupValidationResult.Fail("Yedek manifesti okunamadı.");

            if (!string.Equals(manifest.ProductName, AppConstants.ProductName, StringComparison.Ordinal))
                return BackupValidationResult.Fail("Yedek bu uygulamaya ait değil.");

            if (manifest.SchemaVersion != AppConstants.SchemaVersion)
                return BackupValidationResult.Fail(
                    $"Yedek şema sürümü uyumsuz (yedek: {manifest.SchemaVersion}, uygulama: {AppConstants.SchemaVersion}).");

            var databaseHash = await ComputeEntryHashAsync(databaseEntry, cancellationToken);
            if (!string.Equals(databaseHash, manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                return BackupValidationResult.Fail("Yedek bütünlük kontrolü başarısız: SHA-256 özeti uyuşmuyor.");

            if (settingsEntry is not null)
            {
                using var reader = new StreamReader(settingsEntry.Open(), Encoding.UTF8);
                var settingsJson = await reader.ReadToEndAsync(cancellationToken);
                var settingsHash = ComputeTextHash(settingsJson);
                if (!string.Equals(settingsHash, manifest.SettingsSha256, StringComparison.OrdinalIgnoreCase))
                    return BackupValidationResult.Fail("Ayarlar dosyasının özeti uyuşmuyor.");
            }

            stagedPath = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", $"restore-{Guid.NewGuid():N}.db");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            databaseEntry.ExtractToFile(stagedPath, overwrite: true);

            var integrityError = CheckDatabase(stagedPath);
            if (integrityError is not null)
                return BackupValidationResult.Fail(integrityError);

            var info = new BackupInfo(
                string.Empty,
                Path.GetFileName(zipPath),
                zipPath,
                TimeZoneInfo.ConvertTimeFromUtc(manifest.CreatedAtUtc, TimeZoneInfo.Local),
                new FileInfo(zipPath).Length,
                true,
                databaseHash);

            return BackupValidationResult.Ok(manifest, info);
        }
        catch (Exception ex)
        {
            return BackupValidationResult.Fail($"Yedek doğrulanamadı: {ex.Message}");
        }
        finally
        {
            TryDelete(stagedPath);
            TryDelete(decryptedZip);
        }
    }

    public async Task<BackupInspectionResult> InspectAsync(
        string zipPath,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(zipPath, cancellationToken);
        if (!validation.Success)
            return BackupInspectionResult.Fail(validation.Error ?? "Yedek doğrulanamadı.");

        var stagedPath = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            $"inspect-{Guid.NewGuid():N}.db");
        string? decryptedZip = null;

        try
        {
            var prepared = await PrepareZipAsync(zipPath);
            if (prepared.Error is not null)
                return BackupInspectionResult.Fail(prepared.Error);

            decryptedZip = prepared.TempPath;

            bool hasLogo;
            using (var zip = ZipFile.OpenRead(prepared.Path))
            {
                var databaseEntry = zip.GetEntry(DatabaseEntry);
                if (databaseEntry is null)
                    return BackupInspectionResult.Fail("Yedekte data.db bulunamadı.");

                hasLogo = zip.Entries.Any(x =>
                    x.FullName.StartsWith(LogoEntryPrefix, StringComparison.OrdinalIgnoreCase));

                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                databaseEntry.ExtractToFile(stagedPath, overwrite: true);
            }

            var summary = await BackupContentInspector.ReadAsync(
                stagedPath,
                hasLogo,
                cancellationToken);

            var available = BuildAvailableSelection(summary, hasLogo);
            var warnings = BuildWarnings(validation.Manifest!, summary);

            return BackupInspectionResult.Ok(
                validation.Backup!,
                validation.Manifest!,
                summary,
                available,
                warnings);
        }
        catch (Exception ex)
        {
            return BackupInspectionResult.Fail($"Yedek incelenemedi: {ex.Message}");
        }
        finally
        {
            TryDelete(stagedPath);
            TryDelete(decryptedZip);
        }
    }

    public async Task<BackupRestoreResult> RestoreAsync(
        string zipPath,
        RestoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return BackupRestoreResult.Fail("Geri yükleme yetkisi yalnızca yöneticidedir.");

        var restoreOptions = options ?? RestoreOptions.Default;
        var selection = restoreOptions.Selection.Normalize();

        var inspection = await InspectAsync(zipPath, cancellationToken);
        if (!inspection.Success || inspection.Summary is null)
            return BackupRestoreResult.Fail(inspection.Error ?? "Yedek incelenemedi.");

        if (inspection.Summary.IsEmpty && !restoreOptions.AllowEmptyBackup)
        {
            return BackupRestoreResult.Fail(
                "Yedek boş görünüyor (kullanıcı, randevu ve ayar yok). " +
                "Yine de geri yüklemek için boş yedek onayını işaretleyin.");
        }

        // Geri yükleme öncesi mevcut veri otomatik ve tam olarak yedeklenir.
        var safety = await CreateAsync(BackupTrigger.PreRestore, BackupOptions.Full, cancellationToken);
        if (!safety.Success)
        {
            return BackupRestoreResult.Fail(
                $"Güvenlik yedeği alınamadığı için geri yükleme durduruldu: {safety.Error}");
        }

        var stagedPath = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            $"restore-{Guid.NewGuid():N}.db");
        string? decryptedZip = null;

        try
        {
            var prepared = await PrepareZipAsync(zipPath);
            if (prepared.Error is not null)
                return BackupRestoreResult.Fail(prepared.Error);

            decryptedZip = prepared.TempPath;

            using (var zip = ZipFile.OpenRead(prepared.Path))
            {
                var databaseEntry = zip.GetEntry(DatabaseEntry)
                    ?? throw new InvalidOperationException("Yedekte data.db bulunamadı.");

                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                databaseEntry.ExtractToFile(stagedPath, overwrite: true);
            }

            var integrityError = CheckDatabase(stagedPath);
            if (integrityError is not null)
                return BackupRestoreResult.Fail(integrityError);

            // Eski yedeklerde eksik tablo/kolon olabilir; geçici kopyayı güncel şemaya taşı.
            using (var stagedDb = AppDbContextFactory.Create(stagedPath))
                stagedDb.Database.Migrate();

            RestoreReport report;
            await using (var live = await _dbFactory.CreateDbContextAsync(cancellationToken))
            await using (var transaction = await live.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    var engine = new BackupRestoreEngine(_secrets);
                    report = await engine.MergeAsync(
                        live,
                        stagedPath,
                        restoreOptions with { Selection = selection },
                        _currentUser.UserId ?? string.Empty,
                        logoRestored: false,
                        cancellationToken);

                    // Sunucudan okunan eski yanıtlar geri yükleme sonrası gösterilmesin.
                    await live.SyncCacheEntries.ExecuteDeleteAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }

            if (selection.Has(BackupSection.Logo))
            {
                string? logoWarning = null;
                if (TryRestoreLogo(prepared.Path, out logoWarning))
                    report = report with { LogoRestored = true };
                else if (logoWarning is not null)
                    report = report with { Warnings = report.Warnings.Append(logoWarning).ToList() };
            }

            _settings.InvalidateCache();

            await _audit.LogAsync(
                AuditAction.BackupRestored,
                _currentUser.UserId,
                nameof(BackupRecord),
                inspection.Backup?.FileName,
                JsonSerializer.Serialize(new
                {
                    inspection.Backup?.FileName,
                    sections = selection.Sections.ToString(),
                    settingsGroups = selection.SettingsGroups.ToString(),
                    mode = restoreOptions.AppointmentMode.ToString(),
                    report.AppointmentsAdded,
                    report.AppointmentsUpdated,
                    report.PersonsAdded,
                    report.SettingsApplied,
                }));

            return BackupRestoreResult.Ok(report);
        }
        catch (Exception ex)
        {
            return BackupRestoreResult.Fail($"Geri yükleme başarısız: {ex.Message}");
        }
        finally
        {
            TryDelete(stagedPath);
            TryDelete(decryptedZip);
        }
    }

    private static BackupSelection BuildAvailableSelection(BackupContentSummary summary, bool hasLogo)
    {
        var sections = BackupSection.None;

        if (summary.AppointmentCount > 0)
            sections |= BackupSection.Appointments | BackupSection.Persons | BackupSection.Categories;
        else
        {
            if (summary.PersonCount > 0)
                sections |= BackupSection.Persons;
            if (summary.CategoryCount > 0)
                sections |= BackupSection.Categories;
        }

        if (summary.UserCount > 0)
            sections |= BackupSection.Users;
        if (summary.SettingCount > 0)
            sections |= BackupSection.Settings;
        if (summary.AuditLogCount > 0)
            sections |= BackupSection.AuditLogs;
        if (summary.ShareLogCount > 0)
            sections |= BackupSection.ShareLogs;
        if (summary.BackupRecordCount > 0)
            sections |= BackupSection.BackupHistory;
        if (summary.ContentVersionCount > 0)
            sections |= BackupSection.ContentVersions;
        if (summary.RecipientCount > 0)
            sections |= BackupSection.Recipients;
        if (hasLogo)
            sections |= BackupSection.Logo;

        var settingsGroups = summary.SettingCount > 0
            ? BackupSettingsGroup.All
            : BackupSettingsGroup.None;

        return new BackupSelection(sections, settingsGroups);
    }

    private static IReadOnlyList<string> BuildWarnings(
        BackupManifest manifest,
        BackupContentSummary summary)
    {
        var warnings = new List<string>();

        if (summary.IsEmpty)
            warnings.Add("Bu yedek boş görünüyor: kullanıcı, randevu ve ayar içermiyor.");

        if (summary.UserCount == 0)
            warnings.Add("Bu yedekte kullanıcı hesabı yok; geri yüklemede mevcut kullanıcılar korunur.");

        if (summary.AppointmentCount == 0 && summary.SettingCount > 0)
            warnings.Add("Yedekte randevu yok; yalnızca ayarlar ve diğer kayıtlar var.");

        if (summary.HasSmtpSettings || summary.HasPushSettings)
        {
            warnings.Add(
                "Yedekte şifreli iletişim ayarları var. Yedek başka bir Windows kullanıcısında alındıysa " +
                "SMTP/ntfy/Telegram parolaları çözülemeyebilir.");
        }

        if (manifest.ManifestVersion < 2)
            warnings.Add("Eski yedek biçimi: içerik özeti yedekteki veritabanından hesaplandı.");

        if (!string.Equals(manifest.Version, AppConstants.Version, StringComparison.Ordinal))
        {
            warnings.Add(
                $"Yedek farklı bir sürümde alınmış (yedek: {manifest.Version}, uygulama: {AppConstants.Version}); " +
                "şema otomatik güncellenecek.");
        }

        return warnings;
    }

    private async Task<string> BuildSettingsJsonAsync(
        BackupSelection selection,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var values = await _settingsRepository.GetAllAsync();
        var allowed = BackupSettingKeys.KeysFor(selection.SettingsGroups)
            .Where(x => !BackupSettingKeys.IsSecret(x))
            .Where(x => !BackupSettingKeys.IsAlwaysExcluded(x))
            .ToHashSet(StringComparer.Ordinal);

        var filtered = values
            .Where(x => allowed.Contains(x.Key))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        return JsonSerializer.Serialize(new
        {
            Note = "Gizli alanlar (SMTP parolası, push token/chat id) bu dosyaya yazılmaz; " +
                   "veritabanı yedeğinde şifreli olarak bulunur.",
            Settings = filtered,
        }, JsonOptions);
    }

    private static void ApplySelection(string databasePath, BackupSelection selection)
    {
        using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");
        connection.Open();

        Execute(connection, "PRAGMA foreign_keys = OFF;");

        if (!selection.Has(BackupSection.Appointments))
        {
            DeleteTable(connection, "ReminderDispatches");
            DeleteTable(connection, "AppointmentOutcomes");
            DeleteTable(connection, "Appointments");
        }

        if (!selection.Has(BackupSection.Persons))
            DeleteTable(connection, "Persons");

        if (!selection.Has(BackupSection.Categories))
            DeleteTable(connection, "Categories");

        if (!selection.Has(BackupSection.Users))
            DeleteTable(connection, "Users");

        if (!selection.Has(BackupSection.AuditLogs))
            DeleteTable(connection, "AuditLogs");

        if (!selection.Has(BackupSection.ShareLogs))
            DeleteTable(connection, "WhatsAppShareLogs");

        if (!selection.Has(BackupSection.BackupHistory))
            DeleteTable(connection, "BackupRecords");

        if (!selection.Has(BackupSection.ContentVersions))
            DeleteTable(connection, "ContentVersions");

        if (!selection.Has(BackupSection.Recipients))
            DeleteTable(connection, "Recipients");

        // Sunucu okuma önbelleği ve oturum belirteçleri hiçbir yedeğe girmez.
        DeleteTable(connection, "SyncCacheEntries");
        ApplySettingsSelection(connection, selection);

        Execute(connection, "VACUUM;");
    }

    private static void ApplySettingsSelection(SqliteConnection connection, BackupSelection selection)
    {
        if (!TableExists(connection, "Settings"))
            return;

        var allowed = selection.Has(BackupSection.Settings)
            ? BackupSettingKeys.KeysFor(selection.SettingsGroups)
                .Where(x => !BackupSettingKeys.IsAlwaysExcluded(x))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var keys = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Key FROM Settings;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                keys.Add(reader.GetString(0));
        }

        foreach (var key in keys)
        {
            if (BackupSettingKeys.IsAlwaysExcluded(key) || !allowed.Contains(key))
            {
                using var delete = connection.CreateCommand();
                delete.CommandText = "DELETE FROM Settings WHERE Key = $key;";
                delete.Parameters.AddWithValue("$key", key);
                delete.ExecuteNonQuery();
            }
        }
    }

    private bool TryRestoreLogo(string zipPath, out string? warning)
    {
        warning = null;

        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(x =>
                x.FullName.StartsWith(LogoEntryPrefix, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                return false;

            Directory.CreateDirectory(_environment.BrandingDirectory);

            var fileName = Path.GetFileName(entry.FullName);
            var target = Path.Combine(_environment.BrandingDirectory, fileName);
            var temp = Path.Combine(
                Path.GetTempPath(),
                "CgpRandevuTakip",
                $"logo-{Guid.NewGuid():N}{Path.GetExtension(fileName)}");

            Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
            entry.ExtractToFile(temp, overwrite: true);

            foreach (var existing in Directory.GetFiles(_environment.BrandingDirectory, "institution-logo.*"))
                TryDelete(existing);

            File.Move(temp, target, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            warning = $"Kurum logosu geri yüklenemedi: {ex.Message}";
            return false;
        }
    }

    private string? FindLogo()
    {
        if (!Directory.Exists(_environment.BrandingDirectory))
            return null;

        return Directory
            .GetFiles(_environment.BrandingDirectory, "institution-logo.*")
            .FirstOrDefault();
    }

    private async Task<(string Path, string? TempPath, string? Error)> PrepareZipAsync(string path)
    {
        if (!BackupFileNameBuilder.IsEncrypted(Path.GetFileName(path))
            && !BackupCipher.IsEncryptedFile(path))
        {
            return (path, null, null);
        }

        var settings = await _settings.GetAsync();
        if (string.IsNullOrWhiteSpace(settings.BackupPassword))
            return (path, null, "Şifreli yedeği açmak için yedek parolası gerekli.");

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "CgpRandevuTakip",
            $"decrypt-{Guid.NewGuid():N}.zip");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            BackupCipher.DecryptFile(path, tempPath, settings.BackupPassword);
            return (tempPath, tempPath, null);
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            return (path, null, $"Şifreli yedek açılamadı: {ex.Message}");
        }
    }

    private static string BuildUniquePath(string directory, DateTime localNow, bool encrypted)
    {
        for (var offset = 0; offset < 120; offset++)
        {
            var candidate = Path.Combine(
                directory,
                BackupFileNameBuilder.Build(localNow.AddSeconds(offset), encrypted));

            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(
            directory,
            BackupFileNameBuilder.Build(localNow.AddMinutes(1), encrypted));
    }

    private async Task ApplyRetentionAsync(string directory)
    {
        try
        {
            var files = Directory
                .GetFiles(directory, BackupFileNameBuilder.Prefix + "*")
                .Where(x => BackupFileNameBuilder.IsValid(Path.GetFileName(x)))
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .ToList();

            foreach (var file in files.Skip(RetentionCount))
                TryDelete(file);

            await Task.CompletedTask;
        }
        catch
        {
            // Saklama temizliği başarısız olsa da yedek geçerlidir.
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void DeleteTable(SqliteConnection connection, string table)
    {
        if (!TableExists(connection, table))
            return;

        Execute(connection, $"DELETE FROM \"{table}\";");
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void AddTextEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string? CheckDatabase(string path)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            connection.Open();

            using (var integrity = connection.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = integrity.ExecuteScalar() as string;
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    return $"Veritabanı bütünlük kontrolü başarısız: {result}";
            }

            using (var tables = connection.CreateCommand())
            {
                tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Appointments';";
                var count = Convert.ToInt32(tables.ExecuteScalar());
                if (count == 0)
                    return "Yedekteki veritabanı bu uygulamaya ait değil (Appointments tablosu yok).";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Yedekteki veritabanı açılamadı: {ex.Message}";
        }
    }

    private BackupInfo ToInfo(BackupRecord record)
        => new(
            record.Id,
            record.FileName,
            record.Path,
            _clock.ToLocal(record.CreatedAtUtc),
            record.SizeBytes,
            record.IsVerified,
            record.Sha256);

    private static async Task<string> ComputeFileHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            81920,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task<string> ComputeEntryHashAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string ComputeTextHash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Geçici dosya silinemezse sonraki temizlikte ele alınır.
        }
    }
}
