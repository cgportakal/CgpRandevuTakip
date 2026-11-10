using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// SQLite çevrimiçi kopyası + ayarlar + manifest içeren ZIP yedeği üretir,
/// doğrular ve geri yükler. Yedekler varsayılan olarak son 30 dosyayla sınırlıdır.
/// </summary>
public sealed class BackupService : IBackupService
{
    private const int RetentionCount = 30;
    private const string DatabaseEntry = "data.db";
    private const string SettingsEntry = "settings.json";
    private const string ManifestEntry = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ISettingsService _settings;
    private readonly IAppEnvironment _environment;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IGoogleDriveBackupUploader _driveUploader;

    public BackupService(
        IDbContextFactory<AppDbContext> dbFactory,
        ISettingsService settings,
        IAppEnvironment environment,
        IAuditLogger audit,
        ICurrentUser currentUser,
        IClock clock,
        IGoogleDriveBackupUploader driveUploader)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _environment = environment;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
        _driveUploader = driveUploader;
    }

    public async Task<BackupCreateResult> CreateAsync(
        BackupTrigger trigger,
        CancellationToken cancellationToken = default)
    {
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

            var localNow = _clock.ToLocal(_clock.UtcNow);
            var path = BuildUniquePath(directory, localNow, encrypted);
            partialPath = path;

            Directory.CreateDirectory(Path.GetDirectoryName(tempDb)!);

            // SQLite'ın çevrimiçi yedek API'si: uygulama açıkken tutarlı kopya alır.
            await using (var source = new SqliteConnection($"Data Source={_environment.DatabasePath};Mode=ReadOnly;Pooling=False"))
            await using (var destination = new SqliteConnection($"Data Source={tempDb};Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            var databaseHash = await ComputeFileHashAsync(tempDb, cancellationToken);

            int userCount;
            int personCount;
            int appointmentCount;
            await using (var db = await _dbFactory.CreateDbContextAsync(cancellationToken))
            {
                userCount = await db.Users.CountAsync(cancellationToken);
                personCount = await db.Persons.CountAsync(cancellationToken);
                appointmentCount = await db.Appointments.CountAsync(cancellationToken);
            }

            var settingsJson = SerializeSettings(settings);
            var settingsHash = ComputeTextHash(settingsJson);

            var manifest = new BackupManifest(
                AppConstants.ProductName,
                AppConstants.Version,
                AppConstants.SchemaVersion,
                _clock.UtcNow,
                userCount,
                personCount,
                appointmentCount,
                databaseHash,
                settingsHash);

            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);

            using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tempDb, DatabaseEntry, CompressionLevel.Optimal);
                AddTextEntry(zip, SettingsEntry, settingsJson);
                AddTextEntry(zip, ManifestEntry, manifestJson);
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
                JsonSerializer.Serialize(new { trigger, record.FileName }));

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
                manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd());

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
                manifest = JsonSerializer.Deserialize<BackupManifest>(await reader.ReadToEndAsync(cancellationToken));
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

    public async Task<BackupCreateResult> RestoreAsync(
        string zipPath,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsInRole(UserRole.Admin))
            return BackupCreateResult.Fail("Geri yükleme yetkisi yalnızca yöneticidedir.");

        var validation = await ValidateAsync(zipPath, cancellationToken);
        if (!validation.Success)
            return BackupCreateResult.Fail(validation.Error ?? "Yedek doğrulanamadı.");

        // Geri yükleme öncesi mevcut veri otomatik yedeklenir.
        var safety = await CreateAsync(BackupTrigger.PreRestore, cancellationToken);
        if (!safety.Success)
            return BackupCreateResult.Fail(
                $"Güvenlik yedeği alınamadığı için geri yükleme durduruldu: {safety.Error}");

        var stagedPath = Path.Combine(Path.GetTempPath(), "CgpRandevuTakip", $"restore-{Guid.NewGuid():N}.db");
        var prepared = await PrepareZipAsync(zipPath);
        if (prepared.Error is not null)
            return BackupCreateResult.Fail(prepared.Error);

        try
        {
            using (var zip = ZipFile.OpenRead(prepared.Path))
            {
                var databaseEntry = zip.GetEntry(DatabaseEntry)
                    ?? throw new InvalidOperationException("Yedekte data.db bulunamadı.");
                Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                databaseEntry.ExtractToFile(stagedPath, overwrite: true);
            }

            var integrityError = CheckDatabase(stagedPath);
            if (integrityError is not null)
                return BackupCreateResult.Fail(integrityError);

            SqliteConnection.ClearAllPools();
            DeleteIfExists(_environment.DatabasePath + "-wal");
            DeleteIfExists(_environment.DatabasePath + "-shm");
            DeleteIfExists(_environment.DatabasePath + "-journal");

            File.Copy(stagedPath, _environment.DatabasePath, overwrite: true);

            await _audit.LogAsync(
                AuditAction.BackupRestored,
                _currentUser.UserId,
                nameof(BackupRecord),
                validation.Backup?.FileName,
                JsonSerializer.Serialize(new { validation.Backup?.FileName }));

            return BackupCreateResult.Ok(validation.Backup!);
        }
        catch (Exception ex)
        {
            return BackupCreateResult.Fail($"Geri yükleme başarısız: {ex.Message}");
        }
        finally
        {
            TryDelete(stagedPath);
            TryDelete(prepared.TempPath);
        }
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
                .GetFiles(directory, BackupFileNameBuilder.Prefix + "*" + BackupFileNameBuilder.Extension)
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

    private static string SerializeSettings(AppSettings settings)
        => JsonSerializer.Serialize(new
        {
            settings.InstitutionName,
            Theme = settings.Theme.ToString(),
            settings.BackupDirectory,
            settings.NotificationsEnabled,
            settings.DefaultReminderMinutes,
            settings.PushEnabled,
            settings.PushProvider,
            settings.PushTopic,
            settings.DailyShareTime,
            settings.QuietHoursEnabled,
            settings.QuietHoursStart,
            settings.QuietHoursEnd,
            settings.WorkHoursStart,
            settings.WorkHoursEnd,
            settings.AllowOutOfHoursAppointments,
            settings.AutoBackupEnabled,
            settings.AutoStartEnabled,
            settings.AutoStartMode,
            settings.CloseToTray,
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUseSsl,
            settings.SmtpUsername,
            settings.SmtpFromAddress,
            settings.SmtpFromName,
            Note = "Gizli alanlar (SMTP parolası, push token/chat id) bu dosyaya yazılmaz; veritabanı yedeğinde şifreli olarak bulunur."
        }, JsonOptions);

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

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Dosya kilitliyse yeni veri yazılamaz; hata üst katmana döner.
        }
    }

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
