using System.Globalization;
using System.Text.Json;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Content;

/// <summary>
/// Web iÃ§erik yÃ¶netim konsolunun uygulama servisi. Kategori/gÃ¶rÃ¼ÅŸmeci/ayar
/// dÃ¼zenlemelerini mevcut servisler Ã¼zerinden yapar; yayÄ±nlama ve geri alma
/// iÅŸlemleri iÃ§in iÃ§erik anlÄ±k gÃ¶rÃ¼ntÃ¼leri saklar.
/// </summary>
public sealed class ContentAdminService : IContentAdminService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] LogoExtensions = [".png", ".jpg", ".jpeg", ".bmp"];
    private const long MaxLogoSizeBytes = 4 * 1024 * 1024;

    private readonly ICategoryService _categoryService;
    private readonly IHostService _hostService;
    private readonly ISettingsService _settingsService;
    private readonly ICategoryRepository _categories;
    private readonly IUserRepository _users;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IContentVersionRepository _versions;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IAppEnvironment _environment;

    public ContentAdminService(
        ICategoryService categoryService,
        IHostService hostService,
        ISettingsService settingsService,
        ICategoryRepository categories,
        IUserRepository users,
        ISettingsRepository settingsRepository,
        IContentVersionRepository versions,
        IAuditLogger audit,
        ICurrentUser currentUser,
        IClock clock,
        IAppEnvironment environment)
    {
        _categoryService = categoryService;
        _hostService = hostService;
        _settingsService = settingsService;
        _categories = categories;
        _users = users;
        _settingsRepository = settingsRepository;
        _versions = versions;
        _audit = audit;
        _currentUser = currentUser;
        _clock = clock;
        _environment = environment;
    }

    public async Task<ContentAdminDto> GetAsync()
    {
        var settings = await _settingsService.GetAsync();
        var categories = await _categoryService.GetAllAsync();
        var hosts = await _hostService.GetAllAsync();
        var versions = await _versions.GetRecentAsync(20);
        var current = versions.FirstOrDefault();

        return new ContentAdminDto(
            ToInstitution(settings),
            ToSettings(settings),
            categories,
            hosts,
            current is null ? null : ToVersionDto(current, current.Id),
            versions.Select(version => ToVersionDto(version, current?.Id)).ToList());
    }

    public async Task<OperationResult> SaveSettingsAsync(ContentSettingsInput input)
    {
        if (!IsAdmin())
            return OperationResult.Fail("Ä°Ã§erik ayarlarÄ±nÄ± yalnÄ±zca yÃ¶netici deÄŸiÅŸtirebilir.");

        var institutionName = input.InstitutionName?.Trim() ?? string.Empty;
        var institutionEmail = input.InstitutionEmail?.Trim() ?? string.Empty;
        var institutionPhone = input.InstitutionPhone?.Trim() ?? string.Empty;
        var institutionAddress = input.InstitutionAddress?.Trim() ?? string.Empty;

        var validation = InstitutionProfilePolicy.ValidateName(institutionName, required: true)
                         ?? InstitutionProfilePolicy.ValidateEmail(institutionEmail, required: false)
                         ?? InstitutionProfilePolicy.ValidatePhone(institutionPhone, required: false)
                         ?? InstitutionProfilePolicy.ValidateAddress(institutionAddress, required: false);

        if (validation is not null)
            return OperationResult.Fail(validation);

        if (!_settingsService.AllowedReminderMinutes.Contains(input.DefaultReminderMinutes))
            return OperationResult.Fail("VarsayÄ±lan hatÄ±rlatÄ±cÄ± 30 dk, 1 saat veya 1 gÃ¼n olmalÄ±dÄ±r.");

        if (!IsTime(input.WorkHoursStart) || !IsTime(input.WorkHoursEnd))
            return OperationResult.Fail("Ã‡alÄ±ÅŸma saatleri SS:dd biÃ§iminde olmalÄ±dÄ±r.");

        if (TimeSpan.ParseExact(input.WorkHoursStart, @"hh\:mm", CultureInfo.InvariantCulture)
            >= TimeSpan.ParseExact(input.WorkHoursEnd, @"hh\:mm", CultureInfo.InvariantCulture))
        {
            return OperationResult.Fail("Ã‡alÄ±ÅŸma saati baÅŸlangÄ±cÄ± bitiÅŸten Ã¶nce olmalÄ±dÄ±r.");
        }

        if (!IsTime(input.QuietHoursStart) || !IsTime(input.QuietHoursEnd))
            return OperationResult.Fail("Sessiz saatler SS:dd biÃ§iminde olmalÄ±dÄ±r.");

        if (!IsTime(input.DailyShareTime))
            return OperationResult.Fail("GÃ¼nlÃ¼k paylaÅŸÄ±m saati SS:dd biÃ§iminde olmalÄ±dÄ±r.");

        await _settingsRepository.SetManyAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SettingsKeys.InstitutionName] = institutionName,
            [SettingsKeys.InstitutionEmail] = institutionEmail,
            [SettingsKeys.InstitutionPhone] = institutionPhone,
            [SettingsKeys.InstitutionAddress] = institutionAddress,
            [SettingsKeys.DefaultReminderMinutes] = input.DefaultReminderMinutes.ToString(CultureInfo.InvariantCulture),
            [SettingsKeys.WorkHoursStart] = input.WorkHoursStart,
            [SettingsKeys.WorkHoursEnd] = input.WorkHoursEnd,
            [SettingsKeys.AllowOutOfHoursAppointments] = input.AllowOutOfHoursAppointments.ToString(),
            [SettingsKeys.NotificationsEnabled] = input.NotificationsEnabled.ToString(),
            [SettingsKeys.PushEnabled] = input.PushEnabled.ToString(),
            [SettingsKeys.QuietHoursEnabled] = input.QuietHoursEnabled.ToString(),
            [SettingsKeys.QuietHoursStart] = input.QuietHoursStart,
            [SettingsKeys.QuietHoursEnd] = input.QuietHoursEnd,
            [SettingsKeys.DailyShareTime] = input.DailyShareTime
        });

        await _audit.LogAsync(AuditAction.SettingsChanged, _currentUser.UserId);
        return OperationResult.Ok();
    }

    public async Task<ContentLogoResult> SaveLogoAsync(
        string extension,
        long length,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
            return ContentLogoResult.Fail("Logoyu yalnızca yönetici değiştirebilir.");

        var normalized = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (!LogoExtensions.Contains(normalized))
            return ContentLogoResult.Fail("Desteklenmeyen logo türü. PNG, JPG veya BMP yükleyin.");

        if (length <= 0)
            return ContentLogoResult.Fail("Logo dosyası boş.");

        if (length > MaxLogoSizeBytes)
            return ContentLogoResult.Fail("Logo dosyası en fazla 4 MB olabilir.");

        try
        {
            Directory.CreateDirectory(_environment.BrandingDirectory);
            var fileName = $"institution-logo{normalized}";
            var targetPath = Path.Combine(_environment.BrandingDirectory, fileName);

            foreach (var existing in Directory.EnumerateFiles(
                         _environment.BrandingDirectory,
                         "institution-logo.*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(existing, targetPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(existing);
            }

            await using (var target = File.Create(targetPath))
            {
                await content.CopyToAsync(target, cancellationToken);
            }

            var relativePath = $"branding/{fileName}";
            await _settingsRepository.SetManyAsync(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SettingsKeys.InstitutionLogoFile] = relativePath
            });

            await _audit.LogAsync(
                AuditAction.ContentLogoUpdated,
                _currentUser.UserId,
                nameof(AppSettings),
                null,
                relativePath);

            return ContentLogoResult.Ok(ResolveLogoUrl(relativePath) ?? "/v1/content/logo");
        }
        catch (Exception ex)
        {
            return ContentLogoResult.Fail($"Logo kaydedilemedi: {ex.Message}");
        }
    }

    public async Task<ContentPublishResult> PublishAsync(string? note)
    {
        if (!IsAdmin())
            return ContentPublishResult.Fail("Ä°Ã§eriÄŸi yalnÄ±zca yÃ¶netici yayÄ±nlayabilir.");

        return await PublishInternalAsync(note);
    }

    public async Task<ContentPublishResult> RestoreAsync(string versionId)
    {
        if (!IsAdmin())
            return ContentPublishResult.Fail("Ä°Ã§eriÄŸi yalnÄ±zca yÃ¶netici geri alabilir.");

        var version = await _versions.GetByIdAsync(versionId);
        if (version is null)
            return ContentPublishResult.Fail("Ä°Ã§erik sÃ¼rÃ¼mÃ¼ bulunamadÄ±.");

        ContentSnapshotDto? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ContentSnapshotDto>(version.SnapshotJson, JsonOptions);
        }
        catch
        {
            snapshot = null;
        }

        if (snapshot is null)
            return ContentPublishResult.Fail("Ä°Ã§erik sÃ¼rÃ¼mÃ¼ okunamadÄ±.");

        await RestoreCategoriesAsync(snapshot.Categories);
        await RestoreHostsAsync(snapshot.Hosts);
        await RestoreSettingsAsync(snapshot.Institution, snapshot.Settings);

        await _audit.LogAsync(
            AuditAction.ContentRestored,
            _currentUser.UserId,
            nameof(ContentVersion),
            version.Id,
            JsonSerializer.Serialize(new
            {
                sourceVersion = version.VersionNumber,
                note = version.Note
            }, JsonOptions));

        return await PublishInternalAsync($"Geri alÄ±ndÄ±: v{version.VersionNumber}");
    }

    private async Task<ContentPublishResult> PublishInternalAsync(string? note)
    {
        var snapshot = await CaptureSnapshotAsync();
        var nextVersion = await _versions.GetNextVersionNumberAsync();
        var noteText = note?.Trim();
        var version = new ContentVersion
        {
            VersionNumber = nextVersion,
            PublishedAtUtc = _clock.UtcNow,
            PublishedByUserId = _currentUser.UserId,
            PublishedByUserName = _currentUser.DisplayName,
            Note = string.IsNullOrEmpty(noteText) ? null : noteText[..Math.Min(noteText.Length, 200)],
            SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions)
        };

        await _versions.AddAsync(version);

        await _audit.LogAsync(
            AuditAction.ContentPublished,
            _currentUser.UserId,
            nameof(ContentVersion),
            version.Id,
            JsonSerializer.Serialize(new
            {
                version = version.VersionNumber,
                note = version.Note
            }, JsonOptions));

        return ContentPublishResult.Ok(ToVersionDto(version, version.Id));
    }

    private async Task<ContentSnapshotDto> CaptureSnapshotAsync()
    {
        var settings = await _settingsService.GetAsync();
        var categories = await _categories.GetAllAsync();
        var hosts = (await _hostService.GetAllAsync())
            .Where(host => host.Role == UserRole.Host)
            .ToList();

        return new ContentSnapshotDto(
            ToInstitution(settings),
            ToSettings(settings),
            categories
                .Select(category => new ContentCategorySnapshot(
                    category.Id,
                    category.Name,
                    category.ColorHex,
                    category.SortOrder,
                    category.IsActive))
                .ToList(),
            hosts
                .Select(host => new ContentHostSnapshot(
                    host.Id,
                    host.DisplayName,
                    host.Role,
                    host.IsActive))
                .ToList());
    }

    private async Task RestoreCategoriesAsync(IReadOnlyList<ContentCategorySnapshot> snapshot)
    {
        var existing = (await _categories.GetAllAsync()).ToList();
        var snapshotIds = snapshot.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var snapshotNames = snapshot.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = new List<Category>();

        foreach (var item in snapshot)
        {
            var category = existing.FirstOrDefault(x => x.Id == item.Id)
                           ?? existing.FirstOrDefault(x =>
                               string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase));

            if (category is null)
            {
                await _categories.AddAsync(new Category
                {
                    Id = item.Id,
                    Name = item.Name,
                    ColorHex = item.ColorHex,
                    SortOrder = item.SortOrder,
                    IsActive = item.IsActive,
                    IsDeleted = false
                });
                continue;
            }

            category.Name = item.Name;
            category.ColorHex = item.ColorHex;
            category.SortOrder = item.SortOrder;
            category.IsActive = item.IsActive;
            category.IsDeleted = false;
            changed.Add(category);
        }

        foreach (var category in existing.Where(x =>
                     !snapshotIds.Contains(x.Id)
                     && !snapshotNames.Contains(x.Name)
                     && x.IsActive))
        {
            category.IsActive = false;
            changed.Add(category);
        }

        if (changed.Count > 0)
            await _categories.SaveRangeAsync(changed);
    }

    private async Task RestoreHostsAsync(IReadOnlyList<ContentHostSnapshot> snapshot)
    {
        var users = (await _users.GetAllAsync()).ToList();
        var snapshotHosts = snapshot
            .Where(item => item.Role == UserRole.Host)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var snapshotIds = snapshotHosts.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var item in snapshotHosts.Values)
        {
            var user = users.FirstOrDefault(x => x.Id == item.Id);
            if (user is null || user.Role != UserRole.Host)
                continue;

            if (user.DisplayName == item.DisplayName && user.IsActive == item.IsActive)
                continue;

            user.DisplayName = item.DisplayName;
            user.IsActive = item.IsActive;
            await _users.SaveAsync(user);
        }

        foreach (var user in users.Where(x =>
                     x.Role == UserRole.Host
                     && x.IsActive
                     && !snapshotIds.Contains(x.Id)))
        {
            user.IsActive = false;
            await _users.SaveAsync(user);
        }
    }

    private Task RestoreSettingsAsync(ContentInstitutionDto institution, ContentSettingsDto settings)
        => _settingsRepository.SetManyAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SettingsKeys.InstitutionName] = institution.Name,
            [SettingsKeys.InstitutionEmail] = institution.Email,
            [SettingsKeys.InstitutionPhone] = institution.Phone,
            [SettingsKeys.InstitutionAddress] = institution.Address,
            [SettingsKeys.DefaultReminderMinutes] = settings.DefaultReminderMinutes.ToString(CultureInfo.InvariantCulture),
            [SettingsKeys.WorkHoursStart] = settings.WorkHoursStart,
            [SettingsKeys.WorkHoursEnd] = settings.WorkHoursEnd,
            [SettingsKeys.AllowOutOfHoursAppointments] = settings.AllowOutOfHoursAppointments.ToString(),
            [SettingsKeys.NotificationsEnabled] = settings.NotificationsEnabled.ToString(),
            [SettingsKeys.PushEnabled] = settings.PushEnabled.ToString(),
            [SettingsKeys.QuietHoursEnabled] = settings.QuietHoursEnabled.ToString(),
            [SettingsKeys.QuietHoursStart] = settings.QuietHoursStart,
            [SettingsKeys.QuietHoursEnd] = settings.QuietHoursEnd,
            [SettingsKeys.DailyShareTime] = settings.DailyShareTime
        });

    private ContentInstitutionDto ToInstitution(AppSettings settings)
        => new(
            settings.InstitutionName,
            settings.InstitutionEmail,
            settings.InstitutionPhone,
            settings.InstitutionAddress,
            ResolveLogoUrl(settings.InstitutionLogoFile));

    private string? ResolveLogoUrl(string logoFile)
    {
        if (string.IsNullOrWhiteSpace(logoFile))
            return null;

        try
        {
            if (!Directory.Exists(_environment.BrandingDirectory))
                return "/v1/content/logo";

            var file = Directory
                .EnumerateFiles(_environment.BrandingDirectory, "institution-logo.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (file is null)
                return "/v1/content/logo";

            var stamp = File.GetLastWriteTimeUtc(file).Ticks;
            return $"/v1/content/logo?v={stamp}";
        }
        catch
        {
            return "/v1/content/logo";
        }
    }

    private static ContentSettingsDto ToSettings(AppSettings settings)
        => new(
            settings.DefaultReminderMinutes,
            settings.WorkHoursStart,
            settings.WorkHoursEnd,
            settings.AllowOutOfHoursAppointments,
            settings.NotificationsEnabled,
            settings.PushEnabled,
            settings.QuietHoursEnabled,
            settings.QuietHoursStart,
            settings.QuietHoursEnd,
            settings.DailyShareTime);

    private ContentVersionDto ToVersionDto(ContentVersion version, string? currentId)
    {
        var counts = ReadCounts(version.SnapshotJson);

        return new ContentVersionDto(
            version.Id,
            version.VersionNumber,
            _clock.ToLocal(version.PublishedAtUtc),
            string.IsNullOrWhiteSpace(version.PublishedByUserName) ? "â€”" : version.PublishedByUserName,
            version.Note,
            counts.Categories,
            counts.Hosts,
            version.Id == currentId);
    }

    private static (int Categories, int Hosts) ReadCounts(string json)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ContentSnapshotDto>(json, JsonOptions);
            return snapshot is null
                ? (0, 0)
                : (snapshot.Categories.Count, snapshot.Hosts.Count);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static bool IsTime(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out _);

    private bool IsAdmin() => _currentUser.IsInRole(UserRole.Admin);
}
