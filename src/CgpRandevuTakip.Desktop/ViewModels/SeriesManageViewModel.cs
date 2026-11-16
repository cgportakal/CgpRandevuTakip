using System.Collections.ObjectModel;
using System.Globalization;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>Seriye kayıt ekleme ve seriyi düzenleme penceresi.</summary>
public partial class SeriesManageViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly ICategoryService _categories;
    private readonly IHostService _hosts;
    private readonly Services.IDialogService _dialogs;

    private AppointmentDetailDto? _detail;

    public SeriesManageViewModel(
        IAppointmentService appointments,
        ICategoryService categories,
        IHostService hosts,
        Services.IDialogService dialogs)
    {
        _appointments = appointments;
        _categories = categories;
        _hosts = hosts;
        _dialogs = dialogs;

        AppendCountOptions = new ObservableCollection<int>(Enumerable.Range(1, 12));

        Durations = new ObservableCollection<DurationOption>
        {
            new(15, "15 dakika"),
            new(30, "30 dakika"),
            new(45, "45 dakika"),
            new(60, "1 saat"),
            new(90, "1,5 saat"),
            new(120, "2 saat")
        };

        Methods = new ObservableCollection<MethodOption>
        {
            new(AppointmentMethod.FaceToFace, "Yüz yüze"),
            new(AppointmentMethod.Phone, "Telefon"),
            new(AppointmentMethod.WhatsApp, "WhatsApp mesajı"),
            new(AppointmentMethod.Online, "İnternet")
        };

        Categories = new RangeObservableCollection<CategoryDto>();
        Hosts = new RangeObservableCollection<HostDto>();
        AppendPreview = new RangeObservableCollection<SeriesSlotView>();
    }

    public ObservableCollection<int> AppendCountOptions { get; }

    public ObservableCollection<DurationOption> Durations { get; }

    public ObservableCollection<MethodOption> Methods { get; }

    public RangeObservableCollection<CategoryDto> Categories { get; }

    public RangeObservableCollection<HostDto> Hosts { get; }

    public RangeObservableCollection<SeriesSlotView> AppendPreview { get; }

    public string Title => _detail is null ? "Seri yönetimi" : $"Seri yönetimi · {_detail.PersonFullName}";

    public string SeriesText => _detail?.SeriesText ?? string.Empty;

    [ObservableProperty]
    private int _appendCount = 1;

    [ObservableProperty]
    private string? _appendSummary;

    [ObservableProperty]
    private bool _isAppendBusy;

    [ObservableProperty]
    private HostDto? _selectedHost;

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private DurationOption? _selectedDuration;

    [ObservableProperty]
    private MethodOption? _selectedMethod;

    [ObservableProperty]
    private string _timeText = "10:00";

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _studentFirstName;

    [ObservableProperty]
    private string? _studentLastName;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public async Task InitializeAsync(AppointmentDetailDto detail)
    {
        _detail = detail;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SeriesText));

        AppendCount = 1;
        AppendSummary = null;
        AppendPreview.Clear();

        TryParseTime(detail.StartsAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture), out var time);
        TimeText = $"{time.Hours:00}:{time.Minutes:00}";
        SelectedDuration = Durations.FirstOrDefault(x => x.Minutes == detail.DurationMinutes)
                           ?? Durations.First(x => x.Minutes == 60);
        SelectedMethod = Methods.FirstOrDefault(x => x.Value == detail.Method);
        Description = detail.Description;
        StudentFirstName = detail.StudentFirstName;
        StudentLastName = detail.StudentLastName;

        var categoriesTask = _categories.GetActiveAsync();
        var hostsTask = _hosts.GetSelectableHostsAsync();
        await Task.WhenAll(categoriesTask, hostsTask);

        Categories.ReplaceWith(await categoriesTask);
        Hosts.ReplaceWith(await hostsTask);

        SelectedHost = Hosts.FirstOrDefault(x => x.Id == detail.HostUserId);
        SelectedCategory = Categories.FirstOrDefault(x => x.Id == detail.CategoryId);
    }

    [RelayCommand]
    private async Task PreviewAppendAsync()
    {
        if (_detail?.SeriesId is null)
            return;

        ErrorMessage = null;
        AppendPreview.Clear();
        AppendSummary = null;
        IsAppendBusy = true;

        try
        {
            var plan = await _appointments.PreviewAppendAsync(_detail.SeriesId, AppendCount);
            AppendPreview.ReplaceWith(plan.Slots.Select(ToSlotView));
            AppendSummary = plan.Summary;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Seri önizlenemedi: {ex.Message}";
        }
        finally
        {
            IsAppendBusy = false;
        }
    }

    [RelayCommand]
    private async Task AppendAsync()
    {
        if (_detail?.SeriesId is null)
            return;

        ErrorMessage = null;
        IsAppendBusy = true;

        try
        {
            var plan = await _appointments.PreviewAppendAsync(_detail.SeriesId, AppendCount);
            if (!plan.CanSave)
            {
                ErrorMessage = plan.Error ?? "Eklenecek uygun tarih yok.";
                return;
            }

            if (plan.SkippedCount > 0
                && !_dialogs.Confirm(
                    $"{plan.Summary}{Environment.NewLine}{Environment.NewLine}Devam edilsin mi?",
                    "Seriye kayıt ekle"))
            {
                return;
            }

            var result = await _appointments.AppendSeriesAsync(_detail.SeriesId, AppendCount);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            _dialogs.Info(result.Summary, "Seriye kayıt eklendi");
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Seriye kayıt eklenemedi: {ex.Message}";
        }
        finally
        {
            IsAppendBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (_detail?.SeriesId is null)
            return;

        ErrorMessage = null;

        if (SelectedHost is null || SelectedCategory is null)
        {
            ErrorMessage = "Görüşmeci ve kategori seçilmelidir.";
            return;
        }

        if (!TryParseTime(TimeText, out var time))
        {
            ErrorMessage = "Saat SS:dd biçiminde olmalıdır (ör. 14:30).";
            return;
        }

        if (!_dialogs.Confirm(
                "Bu serideki gelecek tarihli planlanmış görüşmeler güncellenecek. Geçmiş ve kapanmış kayıtlar korunur." +
                $"{Environment.NewLine}{Environment.NewLine}Devam edilsin mi?",
                "Seriyi düzenle"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _appointments.UpdateSeriesAsync(
                _detail.SeriesId,
                new SeriesUpdateInput(
                    SelectedHost.Id,
                    SelectedCategory.Id,
                    SelectedDuration?.Minutes ?? 60,
                    SelectedMethod?.Value ?? AppointmentMethod.FaceToFace,
                    Description,
                    StudentFirstName,
                    StudentLastName,
                    time,
                    _detail.ReminderOffsetsMinutes));

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            var message = result.Summary;
            if (result.Warnings.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine +
                           string.Join(Environment.NewLine, result.Warnings.Select(x => $"• {x}"));
            }

            _dialogs.Info(message, "Seri güncellendi");
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Seri güncellenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private static SeriesSlotView ToSlotView(SeriesSlotDto slot)
        => new(slot.PositionText, slot.DateText, slot.TimeText, slot.StateText, slot.WillCreate);

    private static bool TryParseTime(string? text, out TimeSpan time)
        => TimeSpan.TryParseExact(
            text?.Trim(),
            new[] { "h\\:mm", "hh\\:mm", "H\\:mm" },
            CultureInfo.InvariantCulture,
            out time);
}
