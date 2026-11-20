using CgpRandevuTakip.Application.Backups;
using CgpRandevuTakip.Desktop.ViewModels;

namespace CgpRandevuTakip.Desktop.Tests;

public class BackupViewModelTests
{
    [Fact]
    public void CreateViewModel_DefaultSelection_IsFullBackup()
    {
        var viewModel = new BackupCreateViewModel(new FakeBackupService(EmptyInspection()));

        var selection = viewModel.Selection.Normalize();

        Assert.True(selection.Has(BackupSection.Appointments));
        Assert.True(selection.Has(BackupSection.Users));
        Assert.True(selection.Has(BackupSection.Settings));
        Assert.True(selection.Has(BackupSection.AuditLogs));
        Assert.True(selection.Has(BackupSection.Logo));
        Assert.Equal(BackupSettingsGroup.All, selection.SettingsGroups);
    }

    [Fact]
    public async Task RestoreViewModel_DefaultsToDataOnly()
    {
        var inspection = Inspection(
            appointments: 3,
            users: 2,
            settings: 5,
            hasSettings: true);

        var viewModel = new BackupRestoreViewModel(new FakeBackupService(inspection));
        await viewModel.InitializeAsync(Backup());

        Assert.True(viewModel.RestoreAppointments);
        Assert.False(viewModel.RestoreUsers);
        Assert.False(viewModel.RestoreSettings);

        var selection = viewModel.Selection.Normalize();
        Assert.True(selection.Has(BackupSection.Appointments));
        Assert.True(selection.Has(BackupSection.Persons));
        Assert.True(selection.Has(BackupSection.Categories));
        Assert.False(selection.Has(BackupSection.Users));
        Assert.False(selection.Has(BackupSection.Settings));
        Assert.True(viewModel.CanRestore);
    }

    [Fact]
    public async Task RestoreViewModel_SettingsWithoutGroup_CannotRestore()
    {
        var viewModel = new BackupRestoreViewModel(
            new FakeBackupService(Inspection(appointments: 1, users: 1, settings: 3, hasSettings: true)));

        await viewModel.InitializeAsync(Backup());
        viewModel.RestoreAppointments = false;
        viewModel.RestoreSettings = true;

        Assert.False(viewModel.CanRestore);

        viewModel.SmtpSettings = true;
        Assert.True(viewModel.CanRestore);
        Assert.True(viewModel.Selection.SettingsGroups.HasFlag(BackupSettingsGroup.Smtp));
    }

    [Fact]
    public async Task RestoreViewModel_EmptyBackup_RequiresConfirmation()
    {
        var viewModel = new BackupRestoreViewModel(
            new FakeBackupService(EmptyInspection()));

        await viewModel.InitializeAsync(Backup());

        Assert.True(viewModel.IsEmptyBackup);
        Assert.Contains("boş", viewModel.WarningsText, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.CanRestore);

        viewModel.RestoreAppointments = true;
        viewModel.AllowEmptyBackup = true;

        // İleri düzey onayla boş yedek geri yüklenebilir (hiçbir veri eklemez).
        Assert.True(viewModel.CanRestore);
    }

    private static BackupInfo Backup()
        => new("b1", "CGP_RandevuTakip_Yedek_2026-11-20_12-30-58.zip", "test.zip",
            DateTime.Today, 1024, true, "hash");

    private static BackupInspectionResult EmptyInspection()
        => Inspection(appointments: 0, users: 0, settings: 0, hasSettings: false);

    private static BackupInspectionResult Inspection(
        int appointments,
        int users,
        int settings,
        bool hasSettings)
    {
        var summary = new BackupContentSummary(
            users,
            0,
            0,
            appointments,
            appointments,
            0,
            0,
            0,
            0,
            settings,
            0,
            0,
            0,
            0,
            null,
            appointments > 0 ? DateTime.Today : null,
            DateTime.Now,
            false,
            hasSettings,
            false,
            false,
            false,
            false,
            false);

        var sections = BackupSection.None;
        if (appointments > 0)
            sections |= BackupSection.Appointments | BackupSection.Persons | BackupSection.Categories;
        if (users > 0)
            sections |= BackupSection.Users;
        if (settings > 0)
            sections |= BackupSection.Settings;

        var available = new BackupSelection(
            sections,
            settings > 0 ? BackupSettingsGroup.All : BackupSettingsGroup.None);

        var warnings = summary.IsEmpty
            ? new[] { "Bu yedek boş görünüyor: kullanıcı, randevu ve ayar içermiyor." }
            : Array.Empty<string>();

        return BackupInspectionResult.Ok(
            Backup(),
            new BackupManifest("CGP - Randevu Takip", "1.8.5", 1, DateTime.UtcNow, users, 0, appointments, "hash", "hash"),
            summary,
            available,
            warnings);
    }

    private sealed class FakeBackupService : IBackupService
    {
        private readonly BackupInspectionResult _inspection;

        public FakeBackupService(BackupInspectionResult inspection)
        {
            _inspection = inspection;
        }

        public Task<BackupCreateResult> CreateAsync(
            BackupTrigger trigger,
            BackupOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BackupCreateResult.Ok(Backup()));

        public Task<IReadOnlyList<BackupInfo>> GetHistoryAsync(
            int count = 50,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BackupInfo>>(new[] { Backup() });

        public Task<BackupValidationResult> ValidateAsync(
            string zipPath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BackupValidationResult.Ok(_inspection.Manifest!, Backup()));

        public Task<BackupInspectionResult> InspectAsync(
            string zipPath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_inspection);

        public Task<BackupRestoreResult> RestoreAsync(
            string zipPath,
            RestoreOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BackupRestoreResult.Ok(new RestoreReport(
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false,
                Array.Empty<string>())));
    }
}
