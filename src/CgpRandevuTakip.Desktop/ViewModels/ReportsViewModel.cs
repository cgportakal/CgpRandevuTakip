using System.Collections.ObjectModel;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Reports;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>Grafikte tek bir çubuğun görünümü; genişlik en yüksek değere göre ölçeklenir.</summary>
public sealed record ReportBarVm(string Label, int Total, double Width, string ValueText, string Tooltip)
{
    public const double MaxWidth = 220;

    public bool HasValue => Total > 0;
}

public sealed record ReportCategoryRowVm(
    string Name,
    string ColorHex,
    int Total,
    int Completed,
    int NoShow,
    double Width,
    string ShareText);

public sealed record ReportHostRowVm(
    string Name,
    int Total,
    int Completed,
    int NoShow,
    int Cancelled,
    double Width,
    string ShareText);

public sealed record ReportHourVm(string Label, int Total, double Width, string Tooltip);

/// <summary>Rapor sayfası: hazır aralıklar, KPI'lar, grafikler ve CSV/PDF dışa aktarma.</summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly IReportService _reports;
    private readonly ICategoryService _categories;
    private readonly IHostService _hosts;
    private readonly ISettingsService _settings;
    private readonly IReportExportService _exports;
    private readonly IDialogService _dialogs;
    private readonly Application.Abstractions.ICurrentUser _currentUser;

    private bool _initialized;

    public ReportsViewModel(
        IReportService reports,
        ICategoryService categories,
        IHostService hosts,
        ISettingsService settings,
        IReportExportService exports,
        IDialogService dialogs,
        Application.Abstractions.ICurrentUser currentUser)
    {
        _reports = reports;
        _categories = categories;
        _hosts = hosts;
        _settings = settings;
        _exports = exports;
        _dialogs = dialogs;
        _currentUser = currentUser;

        CategoryFilters = new RangeObservableCollection<CategoryFilterOption>
        {
            new(null, "Tüm kategoriler")
        };

        HostFilters = new RangeObservableCollection<HostFilterOption>
        {
            new(null, "Tüm görüşmeciler")
        };

        _selectedCategoryFilter = CategoryFilters[0];
        _selectedHostFilter = HostFilters[0];

        var today = DateTime.Today;
        _fromDate = new DateTime(today.Year, today.Month, 1);
        _toDate = today;
    }

    public RangeObservableCollection<CategoryFilterOption> CategoryFilters { get; }

    public RangeObservableCollection<HostFilterOption> HostFilters { get; }

    public RangeObservableCollection<ReportBarVm> Bars { get; } = new();

    public RangeObservableCollection<ReportCategoryRowVm> CategoryRows { get; } = new();

    public RangeObservableCollection<ReportHostRowVm> HostRows { get; } = new();

    public RangeObservableCollection<ReportHourVm> HourRows { get; } = new();

    [ObservableProperty]
    private CategoryFilterOption? _selectedCategoryFilter;

    [ObservableProperty]
    private HostFilterOption? _selectedHostFilter;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private ReportSummaryDto? _summary;

    [ObservableProperty]
    private string _rangeText = string.Empty;

    [ObservableProperty]
    private string _bucketTitle = "Günlük dağılım";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _lastUpdatedText = "—";

    public bool ShowHostFilter => !_currentUser.IsInRole(UserRole.Host);

    public bool HasSummary => Summary is { IsEmpty: false };

    public bool IsEmptyReport => Summary is { IsEmpty: true };

    public string PersonSummary => Summary is null
        ? "—"
        : $"{Summary.UniquePersons} kişi · {Summary.ReturningPersons} tekrar gelen · {Summary.RepeatRequested} tekrar talebi";

    public string TotalDurationSummary => Summary is null
        ? "—"
        : $"Toplam süre: {Summary.TotalDurationText}";

    /// <summary>Sayfa ilk açıldığında filtre listelerini ve raporu yükler.</summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            await LoadAsync();
            return;
        }

        _initialized = true;
        try
        {
            var categoriesTask = _categories.GetAllAsync();
            var hostsTask = _hosts.GetAllAsync();
            await Task.WhenAll(categoriesTask, hostsTask);

            CategoryFilters.ReplaceWith(
                new[] { new CategoryFilterOption(null, "Tüm kategoriler") }
                    .Concat((await categoriesTask).Select(x => new CategoryFilterOption(x.Id, x.Name))));

            if (ShowHostFilter)
            {
                HostFilters.ReplaceWith(
                    new[] { new HostFilterOption(null, "Tüm görüşmeciler") }
                        .Concat((await hostsTask).Select(x => new HostFilterOption(x.Id, x.DisplayName))));
            }

            SelectedCategoryFilter = CategoryFilters[0];
            SelectedHostFilter = HostFilters[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Filtreler yüklenemedi: {ex.Message}";
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = null;

        var from = (FromDate ?? DateTime.Today).Date;
        var to = (ToDate ?? DateTime.Today).Date;
        if (to < from)
            (from, to) = (to, from);

        FromDate = from;
        ToDate = to;

        IsBusy = true;
        try
        {
            var query = new ReportQuery(
                from,
                to,
                SelectedCategoryFilter?.Id,
                ShowHostFilter ? SelectedHostFilter?.Id : null);

            Summary = await _reports.GetSummaryAsync(query);
            RangeText = Summary.RangeText;
            BucketTitle = Summary.BucketLabel;
            LastUpdatedText = $"Son güncelleme: {DateTime.Now:dd/MM/yyyy HH:mm}";
            RefreshVisuals();

            OnPropertyChanged(nameof(HasSummary));
            OnPropertyChanged(nameof(IsEmptyReport));
            OnPropertyChanged(nameof(PersonSummary));
            OnPropertyChanged(nameof(TotalDurationSummary));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyPresetAsync(string? preset)
    {
        var today = DateTime.Today;

        (FromDate, ToDate) = preset switch
        {
            "thisMonth" => (new DateTime(today.Year, today.Month, 1), today),
            "lastMonth" => LastMonth(today),
            "thisYear" => (new DateTime(today.Year, 1, 1), today),
            "last30" => (today.AddDays(-29), today),
            _ => (FromDate, ToDate)
        };

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsvAsync()
    {
        if (Summary is null)
            return;

        var institution = await ResolveInstitutionAsync();
        var dialog = new SaveFileDialog
        {
            Title = "Raporu CSV olarak kaydet",
            Filter = "CSV dosyası (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"CGP_Rapor_{Summary.FromLocalDate:yyyy-MM-dd}_{Summary.ToLocalDate:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _exports.ExportCsv(dialog.FileName, institution, Summary);
            _dialogs.Info($"Dosya kaydedildi:{Environment.NewLine}{dialog.FileName}", "Dışa aktar");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Rapor kaydedilemedi: {ex.Message}", "Dışa aktar");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportPdfAsync()
    {
        if (Summary is null)
            return;

        var institution = await ResolveInstitutionAsync();
        var dialog = new SaveFileDialog
        {
            Title = "Raporu PDF olarak kaydet",
            Filter = "PDF belgesi (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"CGP_Rapor_{Summary.FromLocalDate:yyyy-MM-dd}_{Summary.ToLocalDate:yyyy-MM-dd}.pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _exports.ExportPdf(dialog.FileName, institution, Summary);
            _dialogs.Info($"Dosya kaydedildi:{Environment.NewLine}{dialog.FileName}", "Dışa aktar");
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Rapor kaydedilemedi: {ex.Message}", "Dışa aktar");
        }
    }

    private bool CanExport() => Summary is { IsEmpty: false };

    private void RefreshVisuals()
    {
        if (Summary is null)
        {
            Bars.Clear();
            CategoryRows.Clear();
            HostRows.Clear();
            HourRows.Clear();
            return;
        }

        var maxPoint = Summary.Points.Count == 0 ? 0 : Summary.Points.Max(x => x.Total);
        Bars.ReplaceWith(Summary.Points.Select(x => new ReportBarVm(
            x.Label,
            x.Total,
            Scale(x.Total, maxPoint),
            x.Total.ToString(),
            $"{x.Label}: {x.Total} görüşme · {x.Completed} tamamlandı · {x.NoShow} gelmedi")));

        var maxCategory = Summary.Categories.Count == 0 ? 0 : Summary.Categories.Max(x => x.Total);
        CategoryRows.ReplaceWith(Summary.Categories.Select(x => new ReportCategoryRowVm(
            x.CategoryName,
            x.ColorHex,
            x.Total,
            x.Completed,
            x.NoShow,
            Scale(x.Total, maxCategory),
            Summary.Total == 0 ? "—" : $"{x.Total * 100d / Summary.Total:0.#}%")));

        var maxHost = Summary.Hosts.Count == 0 ? 0 : Summary.Hosts.Max(x => x.Total);
        HostRows.ReplaceWith(Summary.Hosts.Select(x => new ReportHostRowVm(
            x.HostName,
            x.Total,
            x.Completed,
            x.NoShow,
            x.Cancelled,
            Scale(x.Total, maxHost),
            Summary.Total == 0 ? "—" : $"{x.Total * 100d / Summary.Total:0.#}%")));

        var maxHour = Summary.Hours.Count == 0 ? 0 : Summary.Hours.Max(x => x.Total);
        HourRows.ReplaceWith(Summary.Hours.Select(x => new ReportHourVm(
            x.Label,
            x.Total,
            Scale(x.Total, maxHour),
            $"{x.Label} · {x.Total} görüşme")));

        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
    }

    private static double Scale(int value, int max)
        => max <= 0 ? 0 : Math.Max(3, value * ReportBarVm.MaxWidth / max);

    private async Task<string> ResolveInstitutionAsync()
    {
        try
        {
            var settings = await _settings.GetAsync();
            return AppConstants.ResolveInstitutionName(settings.InstitutionName);
        }
        catch
        {
            return AppConstants.DefaultInstitutionName;
        }
    }

    private static (DateTime, DateTime) LastMonth(DateTime today)
    {
        var first = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption? value) => _ = LoadAsync();

    partial void OnSelectedHostFilterChanged(HostFilterOption? value) => _ = LoadAsync();
}
