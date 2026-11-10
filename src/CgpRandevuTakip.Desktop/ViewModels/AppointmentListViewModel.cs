using System.Windows.Threading;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record CategoryFilterOption(string? Id, string Label);

public sealed record StatusFilterOption(AppointmentStatus? Value, string Label);

public sealed record HostFilterOption(string? Id, string Label);

public partial class AppointmentListViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly ICategoryService _categories;
    private readonly IHostService _hosts;
    private readonly Application.Abstractions.ICurrentUser _currentUser;
    private readonly Services.IDialogService _dialogs;
    private readonly Services.IAppointmentDialogService _appointmentDialogs;
    private readonly Services.IAppointmentExportService _exports;
    private readonly Services.IExcelImportService _excelImport;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _searchTimer;
    private readonly List<AppointmentRowViewModel> _selectedRows = new();
    private bool _isAllSelected;
    private bool _applyingSelectAll;

    private bool _filtersLoaded;
    private bool _suppressReload;

    public AppointmentListViewModel(
        IAppointmentService appointments,
        ICategoryService categories,
        IHostService hosts,
        Application.Abstractions.ICurrentUser currentUser,
        Services.IDialogService dialogs,
        Services.IAppointmentDialogService appointmentDialogs,
        Services.IAppointmentExportService exports,
        Services.IExcelImportService excelImport,
        ISettingsService settings)
    {
        _appointments = appointments;
        _categories = categories;
        _hosts = hosts;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _appointmentDialogs = appointmentDialogs;
        _exports = exports;
        _excelImport = excelImport;
        _settings = settings;

        CategoryFilters = new RangeObservableCollection<CategoryFilterOption>
        {
            new(null, "Tüm kategoriler")
        };

        StatusFilters = new RangeObservableCollection<StatusFilterOption>
        {
            new(null, "Tüm durumlar"),
            new(AppointmentStatus.Scheduled, "Planlandı"),
            new(AppointmentStatus.Arrived, "Geldi"),
            new(AppointmentStatus.NoShow, "Gelmedi"),
            new(AppointmentStatus.Postponed, "Ertelendi"),
            new(AppointmentStatus.Cancelled, "İptal"),
            new(AppointmentStatus.Completed, "Tamamlandı")
        };

        _selectedCategoryFilter = CategoryFilters[0];
        _selectedStatusFilter = StatusFilters[0];

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await LoadAsync();
        };

        WeakReferenceMessenger.Default.Register<OpenDateMessage>(
            this,
            static (recipient, message) =>
                ((AppointmentListViewModel)recipient).ApplyDate(message.Date));
    }

    public RangeObservableCollection<AppointmentRowViewModel> Appointments { get; } = new();

    public RangeObservableCollection<CategoryFilterOption> CategoryFilters { get; }

    public RangeObservableCollection<StatusFilterOption> StatusFilters { get; }

    public RangeObservableCollection<HostFilterOption> HostFilters { get; } = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryFilterOption? _selectedCategoryFilter;

    [ObservableProperty]
    private StatusFilterOption? _selectedStatusFilter;

    [ObservableProperty]
    private HostFilterOption? _selectedHostFilter;

    [ObservableProperty]
    private AppointmentRowViewModel? _selectedAppointment;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _summaryText = "0 görüşme";

    [ObservableProperty]
    private string _emptyMessage = "Bu tarihte görüşme yok.";

    [ObservableProperty]
    private bool _hasAppointments;

    [ObservableProperty]
    private AppointmentListItemDto? _nextAppointment;

    public bool HasNextAppointment => NextAppointment is not null;

    public string NextAppointmentText
    {
        get
        {
            if (NextAppointment is null)
                return string.Empty;

            var day = NextAppointment.StartsAtLocal.Date == DateTime.Today
                ? string.Empty
                : $"{NextAppointment.DateLabel} ";

            return $"Sıradaki: {day}{NextAppointment.TimeLabel} — {NextAppointment.PersonFullName}";
        }
    }

    public int SelectedCount => _selectedRows.Count;

    public bool HasSelectedRows => SelectedCount > 0;

    /// <summary>Başlıktaki "tümünü seç" kutusunun durumu.</summary>
    public bool IsAllSelected => _isAllSelected;

    /// <summary>Başlıktaki seçim kutusundan çağrılır.</summary>
    public void SetSelectAll(bool select) => ApplySelectAll(select);

    public string SelectionText => SelectedCount == 1
        ? "1 görüşme seçildi"
        : $"{SelectedCount} görüşme seçildi";

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

    public string DateLabel => SelectedDate.ToString("dd/MM/yyyy dddd");

    public bool CanCreate => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool CanDelete => _currentUser.IsInRole(UserRole.Admin);

    /// <summary>Toplu seçim yalnızca silme veya taşıma yetkisi olanlarda gösterilir.</summary>
    public bool CanBulkSelect => CanCreate || CanDelete;

    public bool CanFilterHosts => !_currentUser.IsInRole(UserRole.Host);

    public bool CanShareDailyList => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool CanExport => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool IsHostRole => _currentUser.IsInRole(UserRole.Host);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await EnsureFiltersAsync();

            var categoryId = SelectedCategoryFilter?.Id;
            var status = SelectedStatusFilter?.Value;
            var useSearch = IsSearchActive;

            var hostUserId = SelectedHostFilter?.Id;
            var rows = useSearch
                ? await _appointments.SearchAsync(SearchText, categoryId, status, null, null, hostUserId)
                : await _appointments.SearchAsync(null, categoryId, status, SelectedDate, SelectedDate, hostUserId);

            foreach (var oldRow in Appointments)
                oldRow.SelectionChanged -= OnRowSelectionChanged;

            _selectedRows.Clear();
            var viewRows = rows.Select(row =>
            {
                var viewRow = new AppointmentRowViewModel(row);
                viewRow.SelectionChanged += OnRowSelectionChanged;
                return viewRow;
            }).ToList();

            Appointments.ReplaceWith(viewRows);
            NotifySelectionChanged();

            HasAppointments = rows.Count > 0;
            EmptyMessage = useSearch
                ? "Aramanızla eşleşen görüşme bulunamadı."
                : "Bu tarihte görüşme yok. Sağ alttaki + düğmesiyle yeni görüşme ekleyebilirsiniz.";

            var completed = 0;
            var noShow = 0;
            foreach (var row in rows)
            {
                if (row.Status == AppointmentStatus.Completed)
                    completed++;
                else if (row.Status == AppointmentStatus.NoShow)
                    noShow++;
            }

            SummaryText = $"{rows.Count} görüşme · {completed} tamamlandı · {noShow} gelmedi";

            NextAppointment = await _appointments.GetNextAppointmentAsync();

            // Yenilemeden sonra seçili satırı yeni örnekle eşleştir.
            if (SelectedAppointment is not null)
            {
                var selectedId = SelectedAppointment.Id;
                SelectedAppointment = Appointments.FirstOrDefault(x => x.Id == selectedId);
            }
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Görüşme listesi yüklenemedi: {ex.Message}", "Günlük liste");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(-1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoTodayAsync()
    {
        SelectedDate = DateTime.Today;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressReload = true;
        SearchText = string.Empty;
        SelectedCategoryFilter = CategoryFilters[0];
        SelectedStatusFilter = StatusFilters[0];
        SelectedHostFilter = HostFilters.Count > 0 ? HostFilters[0] : null;
        _suppressReload = false;
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task OpenNewAsync()
    {
        if (await _appointmentDialogs.ShowCreateAsync(SelectedDate))
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasRowSelection))]
    private async Task OpenDetailAsync()
    {
        if (SelectedAppointment is null)
            return;

        if (await _appointmentDialogs.ShowDetailAsync(SelectedAppointment.Id))
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasNextAppointment))]
    private async Task OpenNextAsync()
    {
        if (NextAppointment is null)
            return;

        if (await _appointmentDialogs.ShowDetailAsync(NextAppointment.Id))
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var targets = GetTargetRows();
        if (targets.Count == 0)
            return;

        var message = targets.Count == 1
            ? $"{targets[0].PersonFullName} · {targets[0].DateLabel} {targets[0].TimeLabel}\n\nGörüşme silinsin mi? 30 gün içinde yönetici tarafından geri alınabilir."
            : $"{targets.Count} görüşme silinsin mi?\n\nSeçilen görüşmeler 30 gün içinde yönetici tarafından geri alınabilir.";

        if (!_dialogs.Confirm(message, targets.Count == 1 ? "Görüşmeyi sil" : "Görüşmeleri sil"))
            return;

        if (targets.Count == 1)
        {
            var single = await _appointments.DeleteAsync(targets[0].Id);
            if (!single.Success)
            {
                _dialogs.Error(single.Error ?? "Görüşme silinemedi.", "Görüşme silinemedi");
                return;
            }
        }
        else
        {
            var bulk = await _appointments.DeleteManyAsync(targets.Select(x => x.Id).ToList());
            if (bulk.Error is not null)
            {
                _dialogs.Error(bulk.Error, "Görüşmeler silinemedi");
                return;
            }

            if (bulk.FailedIds.Count > 0)
                _dialogs.Info(
                    $"{bulk.Deleted} görüşme silindi. {bulk.FailedIds.Count} görüşme silinemedi.",
                    "Toplu silme");
        }

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelected))]
    private async Task MoveSelectedAsync()
    {
        var targets = GetTargetRows();
        if (targets.Count == 0)
            return;

        var request = await _appointmentDialogs.ShowBulkMoveAsync(
            targets.Count,
            SelectedDate.AddDays(1),
            targets.Select(x => x.StartsAtLocal).ToList());

        if (request is null)
            return;

        var ids = targets.Select(x => x.Id).ToList();
        var result = await _appointments.MoveManyAsync(ids, request.NewDate, request.NewTime);
        if (result.Error is not null)
        {
            _dialogs.Error(result.Error, "Toplu taşıma");
            return;
        }

        if (result.HasConflicts)
        {
            var preview = string.Join(
                Environment.NewLine,
                result.Conflicts
                    .Take(4)
                    .Select(x => $"• {x.StartsAtLocal:dd/MM HH:mm} · {x.PersonName}"));

            if (result.Conflicts.Count > 4)
                preview += $"{Environment.NewLine}… ve {result.Conflicts.Count - 4} çakışma daha";

            var force = _dialogs.Confirm(
                $"{result.ConflictingIds.Count} görüşmede çakışma var:{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Yine de taşınsın mı?",
                "Çakışan görüşmeler");

            if (force)
            {
                result = await _appointments.MoveManyAsync(
                    result.ConflictingIds,
                    request.NewDate,
                    request.NewTime,
                    ignoreConflicts: true);
            }
        }

        var skipped = result.ConflictingIds.Count + result.FailedIds.Count;
        var summary = skipped == 0
            ? $"{result.Moved} görüşme {request.NewDate:dd/MM/yyyy} tarihine taşındı."
            : $"{result.Moved} görüşme taşındı. {skipped} görüşme taşınamadı.";

        _dialogs.Info(summary, "Toplu taşıma");
        await LoadAsync();
    }

    [RelayCommand]
    private void ClearSelection() => ApplySelectAll(false);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task OpenDeletedAsync()
    {
        if (await _appointmentDialogs.ShowDeletedAsync())
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanShareDailyList))]
    private async Task ShareDailyListAsync()
        => await _appointmentDialogs.ShowShareDailyListAsync(SelectedDate);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportExcelAsync()
        => await ExportFileAsync(
            "xlsx",
            "Excel çalışma kitabı (*.xlsx)|*.xlsx",
            (path, institution, date, rows) => _exports.ExportExcel(path, institution, date, rows));

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsvAsync()
        => await ExportFileAsync(
            "csv",
            "CSV dosyası (*.csv)|*.csv",
            (path, institution, date, rows) => _exports.ExportCsv(path, institution, date, rows));

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync()
        => await ExportFileAsync(
            "pdf",
            "PDF dosyası (*.pdf)|*.pdf",
            (path, institution, date, rows) => _exports.ExportPdf(path, institution, date, rows));

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task PrintAsync()
    {
        var rows = Appointments.Select(x => x.Item).ToList();
        if (rows.Count == 0)
        {
            _dialogs.Info("Yazdırılacak görüşme yok.", "Yazdır");
            return;
        }

        var settings = await _settings.GetAsync();
        var institution = AppConstants.ResolveInstitutionName(settings.InstitutionName);

        try
        {
            if (_exports.Print(institution, SelectedDate, rows))
                _dialogs.Info("Yazdırma işi gönderildi.", "Yazdır");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Yazdırılamadı: {ex.Message}", "Yazdır");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ImportExcelAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Excel dosyasından görüşme içe aktar",
            Filter = "Excel çalışma kitabı (*.xlsx)|*.xlsx",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        ExcelImportResult result;
        try
        {
            result = await _excelImport.ReadAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Excel okunamadı: {ex.Message}", "İçe aktarma");
            return;
        }

        if (result.Rows.Count == 0)
        {
            var errors = string.Join(
                Environment.NewLine,
                result.Errors.Take(10).Select(x => $"Satır {x.LineNumber}: {x.Message}"));

            _dialogs.Error(
                result.Errors.Count == 0
                    ? "Dosyada aktarılabilir satır bulunamadı."
                    : $"Geçerli satır yok.{Environment.NewLine}{Environment.NewLine}{errors}",
                "İçe aktarma");
            return;
        }

        var errorPreview = result.Errors.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}Hatalı satırlar:{Environment.NewLine}" +
              string.Join(Environment.NewLine, result.Errors.Take(8).Select(x => $"Satır {x.LineNumber}: {x.Message}"));

        if (!_dialogs.Confirm(
                $"{result.Rows.Count} geçerli satır, {result.Errors.Count} hatalı satır bulundu.{errorPreview}" +
                $"{Environment.NewLine}{Environment.NewLine}Geçerli satırlar içe aktarılsın mı?",
                "Excel içe aktarma"))
            return;

        var imported = 0;
        var failed = new List<string>();

        foreach (var row in result.Rows)
        {
            try
            {
                var saved = await _appointments.CreateAsync(row.Input);
                if (saved.Success)
                {
                    imported++;
                }
                else
                {
                    failed.Add($"Satır {row.LineNumber}: {saved.Error}");
                }
            }
            catch (Exception ex)
            {
                failed.Add($"Satır {row.LineNumber}: {ex.Message}");
            }
        }

        var failureText = failed.Count == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}" +
              string.Join(Environment.NewLine, failed.Take(10));

        _dialogs.Info(
            $"{imported} görüşme içe aktarıldı. {failed.Count} satır aktarılamadı.{failureText}",
            "Excel içe aktarma");

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task DownloadImportTemplateAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Excel içe aktarma şablonu",
            Filter = "Excel çalışma kitabı (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = "CGP_Gorusme_Ice_Aktarma_Sablonu.xlsx"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _excelImport.CreateTemplate(dialog.FileName);
            _dialogs.Info($"Şablon kaydedildi:{Environment.NewLine}{dialog.FileName}", "Şablon");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Şablon oluşturulamadı: {ex.Message}", "Şablon");
        }

        await Task.CompletedTask;
    }

    private async Task ExportFileAsync(
        string extension,
        string filter,
        Action<string, string, DateTime, IReadOnlyList<AppointmentListItemDto>> export)
    {
        var rows = Appointments.Select(x => x.Item).ToList();
        if (rows.Count == 0)
        {
            _dialogs.Info("Dışa aktarılacak görüşme yok.", "Dışa aktar");
            return;
        }

        var settings = await _settings.GetAsync();
        var institution = AppConstants.ResolveInstitutionName(settings.InstitutionName);

        var dialog = new SaveFileDialog
        {
            Title = "Günlük listeyi dışa aktar",
            Filter = filter,
            DefaultExt = "." + extension,
            AddExtension = true,
            FileName = $"CGP_Gunluk_Liste_{SelectedDate:yyyy-MM-dd}.{extension}"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            export(dialog.FileName, institution, SelectedDate, rows);
            _dialogs.Info($"Dosya kaydedildi:{Environment.NewLine}{dialog.FileName}", "Dışa aktar");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Dışa aktarma başarısız: {ex.Message}", "Dışa aktar");
        }
    }

    private async Task EnsureFiltersAsync()
    {
        if (_filtersLoaded)
            return;

        var selectedCategoryId = SelectedCategoryFilter?.Id;
        var selectedHostId = SelectedHostFilter?.Id;

        var categoriesTask = _categories.GetAllAsync();
        var hostsTask = _hosts.GetSelectableHostsAsync();
        await Task.WhenAll(categoriesTask, hostsTask);

        var categories = await categoriesTask;
        var categoryOptions = new List<CategoryFilterOption>(categories.Count + 1)
        {
            new(null, "Tüm kategoriler")
        };

        categoryOptions.AddRange(categories.Select(category => new CategoryFilterOption(
            category.Id,
            category.IsActive ? category.Name : $"{category.Name} (pasif)")));

        CategoryFilters.ReplaceWith(categoryOptions);

        var hosts = await hostsTask;
        var hostOptions = new List<HostFilterOption>(hosts.Count + 1)
        {
            new(null, "Tüm görüşmeciler")
        };

        hostOptions.AddRange(hosts.Select(host => new HostFilterOption(host.Id, host.DisplayName)));
        HostFilters.ReplaceWith(hostOptions);

        _suppressReload = true;
        SelectedCategoryFilter = CategoryFilters.FirstOrDefault(x => x.Id == selectedCategoryId)
                                 ?? CategoryFilters[0];
        SelectedHostFilter = HostFilters.FirstOrDefault(x => x.Id == selectedHostId)
                             ?? HostFilters[0];
        _suppressReload = false;

        _filtersLoaded = true;
    }

    private bool HasRowSelection()
        => SelectedAppointment is not null && !IsBusy;

    private bool CanDeleteSelected()
        => CanDelete && !IsBusy && (SelectedCount > 0 || SelectedAppointment is not null);

    private bool CanMoveSelected()
        => CanCreate && !IsBusy && (SelectedCount > 0 || SelectedAppointment is not null);

    /// <summary>Seçim kutuları doluysa onları, değilse odaklanmış satırı hedefler.</summary>
    private List<AppointmentRowViewModel> GetTargetRows()
    {
        if (_selectedRows.Count > 0)
            return _selectedRows.ToList();

        return SelectedAppointment is null
            ? new List<AppointmentRowViewModel>()
            : new List<AppointmentRowViewModel> { SelectedAppointment };
    }

    private void OnRowSelectionChanged(object? sender, EventArgs e)
    {
        if (sender is AppointmentRowViewModel row)
        {
            if (row.IsSelected)
            {
                if (!_selectedRows.Contains(row))
                    _selectedRows.Add(row);
            }
            else
            {
                _selectedRows.Remove(row);
            }
        }

        NotifySelectionChanged();
    }

    private void ApplySelectAll(bool select)
    {
        _applyingSelectAll = true;
        try
        {
            foreach (var row in Appointments)
                row.IsSelected = select;
        }
        finally
        {
            _applyingSelectAll = false;
        }

        NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        var allSelected = Appointments.Count > 0 && SelectedCount == Appointments.Count;
        if (!_applyingSelectAll && _isAllSelected != allSelected)
        {
            _isAllSelected = allSelected;
            OnPropertyChanged(nameof(IsAllSelected));
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelectedRows));
        OnPropertyChanged(nameof(SelectionText));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveSelectedCommand.NotifyCanExecuteChanged();
    }

    private void ApplyDate(DateTime date)
    {
        _suppressReload = true;
        SelectedDate = date.Date;
        _suppressReload = false;
        _ = LoadAsync();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(DateLabel));
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchActive));
        if (_suppressReload)
            return;

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption? value)
    {
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSelectedStatusFilterChanged(StatusFilterOption? value)
    {
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSelectedHostFilterChanged(HostFilterOption? value)
    {
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSelectedAppointmentChanged(AppointmentRowViewModel? value)
    {
        OpenDetailCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnNextAppointmentChanged(AppointmentListItemDto? value)
    {
        OnPropertyChanged(nameof(HasNextAppointment));
        OnPropertyChanged(nameof(NextAppointmentText));
        OpenNextCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OpenDetailCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveSelectedCommand.NotifyCanExecuteChanged();
    }
}
