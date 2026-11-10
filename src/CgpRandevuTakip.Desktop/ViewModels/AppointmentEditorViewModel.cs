using System.Collections.ObjectModel;
using System.Globalization;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Application.Settings;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record MethodOption(AppointmentMethod Value, string Label);

public sealed record DurationOption(int Minutes, string Label);

public partial class AppointmentEditorViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;
    private readonly IHostService _hosts;
    private readonly IAppointmentOutcomeDialogService _outcomeDialogs;
    private readonly IDialogService _dialogs;

    private string? _appointmentId;
    private bool _isClosedAppointment;
    private bool _closedWarningConfirmed;
    private DateTime? _originalStartsAtLocal;
    private int _defaultReminderMinutes = 30;
    private TimeSpan _workHoursStart = AppointmentDefaults.DefaultDayStart;
    private TimeSpan _workHoursEnd = AppointmentDefaults.DefaultDayEnd;
    private bool _allowOutOfHours;

    public AppointmentEditorViewModel(
        IAppointmentService appointments,
        ICategoryService categories,
        ISettingsService settings,
        IHostService hosts,
        IAppointmentOutcomeDialogService outcomeDialogs,
        IDialogService dialogs)
    {
        _appointments = appointments;
        _categories = categories;
        _settings = settings;
        _hosts = hosts;
        _outcomeDialogs = outcomeDialogs;
        _dialogs = dialogs;

        Methods = new ObservableCollection<MethodOption>
        {
            new(AppointmentMethod.FaceToFace, "Yüz yüze"),
            new(AppointmentMethod.Phone, "Telefon"),
            new(AppointmentMethod.WhatsApp, "WhatsApp mesajı"),
            new(AppointmentMethod.Online, "İnternet")
        };

        Durations = new ObservableCollection<DurationOption>
        {
            new(15, "15 dakika"),
            new(30, "30 dakika"),
            new(45, "45 dakika"),
            new(60, "1 saat"),
            new(90, "1,5 saat"),
            new(120, "2 saat")
        };

        _selectedMethod = Methods[0];
        _selectedDuration = Durations.First(x => x.Minutes == 60);

        // İlk değerler; ayarlar yüklendiğinde çalışma saatlerine göre yenilenir.
        TimeOptions = new ObservableCollection<string>(
            AppointmentDefaults.BuildTimeOptions(
                AppointmentDefaults.DefaultDayStart,
                AppointmentDefaults.DefaultDayEnd,
                allowOutOfHours: false));

        ReminderHourOptions = new ObservableCollection<int>(Enumerable.Range(0, 169));
        ReminderMinuteOptions = new ObservableCollection<int>(Enumerable.Range(0, 60));
    }

    public RangeObservableCollection<CategoryDto> Categories { get; } = new();

    public RangeObservableCollection<HostDto> Hosts { get; } = new();

    public ObservableCollection<string> TimeOptions { get; }

    public ObservableCollection<int> ReminderHourOptions { get; }

    public ObservableCollection<int> ReminderMinuteOptions { get; }

    public ObservableCollection<MethodOption> Methods { get; }

    public ObservableCollection<DurationOption> Durations { get; }

    [ObservableProperty]
    private string _title = "Yeni görüşme";

    [ObservableProperty]
    private bool _isEdit;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _lastName = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private DateTime? _date = DateTime.Today;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private DurationOption? _selectedDuration;

    [ObservableProperty]
    private HostDto? _selectedHost;

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private MethodOption? _selectedMethod;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _studentFirstName = string.Empty;

    [ObservableProperty]
    private string _studentLastName = string.Empty;

    [ObservableProperty]
    private bool _reminderOneDay;

    [ObservableProperty]
    private bool _reminderOneHour;

    [ObservableProperty]
    private bool _reminderThirtyMinutes = true;

    [ObservableProperty]
    private bool _reminderCustom;

    [ObservableProperty]
    private int _reminderCustomHours = 1;

    [ObservableProperty]
    private int _reminderCustomMinutes = 30;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _warningMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasOutcome;

    [ObservableProperty]
    private string _outcomeStatusText = "Henüz sonuç girilmedi";

    [ObservableProperty]
    private string _outcomeSummary = string.Empty;

    public bool CanEnterOutcome => IsEdit && !IsBusy;

    public event EventHandler? Saved;

    public async Task InitializeAsync(string? appointmentId, DateTime initialDate)
    {
        _appointmentId = appointmentId;
        IsEdit = appointmentId is not null;
        Title = IsEdit ? "Görüşmeyi düzenle" : "Yeni görüşme";

        await LoadLookupsAsync();

        if (!IsEdit)
        {
            ApplyDefaultStart(initialDate);

            ReminderOneDay = _defaultReminderMinutes == 1440;
            ReminderOneHour = _defaultReminderMinutes == 60;
            ReminderThirtyMinutes = _defaultReminderMinutes == 30;

            SelectedHost = Hosts.FirstOrDefault();
            SelectedCategory = Categories.FirstOrDefault(x => x.IsActive);
            return;
        }

        var detail = await _appointments.GetDetailAsync(appointmentId!);
        if (detail is null)
        {
            ErrorMessage = "Görüşme bulunamadı.";
            return;
        }

        _isClosedAppointment = detail.IsClosed;
        _originalStartsAtLocal = detail.StartsAtLocal;
        FirstName = detail.PersonFirstName;
        LastName = detail.PersonLastName;
        Phone = detail.PhoneE164;
        Date = detail.StartsAtLocal.Date;
        TimeText = detail.StartsAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        EnsureTimeOption(TimeText);
        SelectedDuration = Durations.FirstOrDefault(x => x.Minutes == detail.DurationMinutes);
        if (SelectedDuration is null)
        {
            SelectedDuration = new DurationOption(
                detail.DurationMinutes,
                $"{detail.DurationMinutes} dakika");
            Durations.Add(SelectedDuration);
        }
        SelectedHost = Hosts.FirstOrDefault(x => x.Id == detail.HostUserId);
        SelectedCategory = Categories.FirstOrDefault(x => x.Id == detail.CategoryId);
        if (SelectedCategory is null)
        {
            SelectedCategory = new CategoryDto(
                detail.CategoryId,
                detail.CategoryName,
                detail.CategoryColorHex,
                0,
                IsActive: false,
                0);
            Categories.Add(SelectedCategory);
        }

        SelectedMethod = Methods.FirstOrDefault(x => x.Value == detail.Method) ?? Methods[0];
        Description = detail.Description ?? string.Empty;
        StudentFirstName = detail.StudentFirstName ?? string.Empty;
        StudentLastName = detail.StudentLastName ?? string.Empty;

        ReminderOneDay = detail.ReminderOffsetsMinutes.Contains(1440);
        ReminderOneHour = detail.ReminderOffsetsMinutes.Contains(60);
        ReminderThirtyMinutes = detail.ReminderOffsetsMinutes.Contains(30);

        var customOffset = detail.ReminderOffsetsMinutes
            .FirstOrDefault(x => x is not 30 and not 60 and not 1440);
        if (customOffset > 0)
        {
            ReminderCustom = true;
            ReminderCustomHours = customOffset / 60;
            ReminderCustomMinutes = customOffset % 60;
        }

        var inactiveSelected = SelectedCategory is { IsActive: false };
        if (inactiveSelected)
            WarningMessage = "Bu görüşmenin kategorisi pasif. Kategoriyi değiştirmeden kaydedebilirsiniz.";

        ApplyOutcomeDisplay(detail);
    }

    /// <summary>Var olan bir görüşmeyi şablon alarak yeni randevu taslağı açar.</summary>
    public async Task InitializeRepeatAsync(string sourceAppointmentId, DateTime initialDate)
    {
        _appointmentId = null;
        IsEdit = false;
        Title = "Tekrar randevu";
        _isClosedAppointment = false;
        _closedWarningConfirmed = false;

        await LoadLookupsAsync();

        var source = await _appointments.GetDetailAsync(sourceAppointmentId);
        if (source is null)
        {
            ErrorMessage = "Tekrar edilecek görüşme bulunamadı.";
            return;
        }

        FirstName = source.PersonFirstName;
        LastName = source.PersonLastName;
        Phone = source.PhoneE164;
        ApplyDefaultStart(initialDate);
        SelectedDuration = Durations.FirstOrDefault(x => x.Minutes == source.DurationMinutes)
                           ?? Durations.First(x => x.Minutes == 60);
        SelectedHost = Hosts.FirstOrDefault(x => x.Id == source.HostUserId) ?? Hosts.FirstOrDefault();
        SelectedCategory = Categories.FirstOrDefault(x => x.Id == source.CategoryId);
        if (SelectedCategory is null)
        {
            SelectedCategory = new CategoryDto(
                source.CategoryId,
                source.CategoryName,
                source.CategoryColorHex,
                0,
                IsActive: false,
                0);
            Categories.Add(SelectedCategory);
        }

        SelectedMethod = Methods.FirstOrDefault(x => x.Value == source.Method) ?? Methods[0];
        Description = source.Description ?? string.Empty;
        StudentFirstName = source.StudentFirstName ?? string.Empty;
        StudentLastName = source.StudentLastName ?? string.Empty;

        ReminderOneDay = source.ReminderOffsetsMinutes.Contains(1440);
        ReminderOneHour = source.ReminderOffsetsMinutes.Contains(60);
        ReminderThirtyMinutes = source.ReminderOffsetsMinutes.Contains(30);

        var customOffset = source.ReminderOffsetsMinutes
            .FirstOrDefault(x => x is not 30 and not 60 and not 1440);
        if (customOffset > 0)
        {
            ReminderCustom = true;
            ReminderCustomHours = customOffset / 60;
            ReminderCustomMinutes = customOffset % 60;
        }

        if (SelectedCategory is { IsActive: false })
            WarningMessage = "Önceki görüşmenin kategorisi pasif. Kaydetmek için aktif bir kategori seçin.";

        HasOutcome = false;
        OutcomeSummary = string.Empty;
        OutcomeStatusText = "Yeni randevu";
    }

    [RelayCommand(CanExecute = nameof(CanEnterOutcome))]
    private async Task EnterOutcomeAsync()
    {
        if (_appointmentId is null)
            return;

        if (!await _outcomeDialogs.ShowAsync(_appointmentId))
            return;

        var detail = await _appointments.GetDetailAsync(_appointmentId);
        if (detail is not null)
        {
            ApplyOutcomeDisplay(detail);
            _isClosedAppointment = detail.IsClosed;
            WarningMessage = "Görüşme sonucu kaydedildi.";
        }
    }

    private async Task LoadLookupsAsync()
    {
        var settingsTask = _settings.GetAsync();
        var categoriesTask = _categories.GetAllAsync();
        var hostsTask = _hosts.GetSelectableHostsAsync();

        await Task.WhenAll(settingsTask, categoriesTask, hostsTask);

        var settings = await settingsTask;
        _defaultReminderMinutes = settings.DefaultReminderMinutes;
        _workHoursStart = ParseTime(settings.WorkHoursStart, AppointmentDefaults.DefaultDayStart);
        _workHoursEnd = ParseTime(settings.WorkHoursEnd, AppointmentDefaults.DefaultDayEnd);
        _allowOutOfHours = settings.AllowOutOfHoursAppointments;

        var timeOptions = AppointmentDefaults.BuildTimeOptions(
            _workHoursStart,
            _workHoursEnd,
            _allowOutOfHours);
        TimeOptions.Clear();
        foreach (var option in timeOptions)
            TimeOptions.Add(option);

        // Yeni görüşmede yalnızca aktif kategoriler seçilebilir.
        // Düzenlemede mevcut pasif kategori, kayıt bozulmasın diye ayrıca eklenir.
        var categories = await categoriesTask;
        Categories.ReplaceWith(categories.Where(x => x.IsActive));

        Hosts.ReplaceWith(await hostsTask);
    }

    private void ApplyOutcomeDisplay(AppointmentDetailDto detail)
    {
        HasOutcome = detail.Outcome is not null;
        OutcomeStatusText = detail.Outcome is null
            ? "Henüz sonuç girilmedi"
            : $"{detail.Outcome.AttendanceText} · {(detail.Outcome.WillRepeat ? "Tekrar edilecek" : "Tekrar yok")}";
        OutcomeSummary = detail.Outcome?.SummaryText ?? string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (Date is null)
        {
            ErrorMessage = "Tarih seçilmelidir.";
            return;
        }

        if (!TryParseTime(TimeText, out var time))
        {
            ErrorMessage = "Saat SS:dd biçiminde olmalıdır (ör. 14:30).";
            return;
        }

        var startsAtLocal = Date.Value.Date.Add(time);

        // Geçmiş tarih/saat uyarısı: yeni görüşmede her zaman, düzenlemede yalnızca
        // saat değiştirilip geçmişe alındığında sorulur.
        var timeChangedToPast = !IsEdit || startsAtLocal != _originalStartsAtLocal;
        if (startsAtLocal < DateTime.Now && timeChangedToPast)
        {
            var proceed = _dialogs.Confirm(
                $"Seçilen tarih ve saat geçmişte: {startsAtLocal:dd/MM/yyyy HH:mm}.{Environment.NewLine}{Environment.NewLine}Yine de kaydedilsin mi?",
                "Geçmiş tarih");
            if (!proceed)
                return;
        }

        if (_isClosedAppointment && !_closedWarningConfirmed)
        {
            var proceed = _dialogs.Confirm(
                "Bu görüşme kapanmış. Düzenleme yapmak istediğinize emin misiniz?",
                "Kapanmış görüşme");
            if (!proceed)
                return;

            _closedWarningConfirmed = true;
        }

        var offsets = new List<int>();
        if (ReminderOneDay)
            offsets.Add(1440);
        if (ReminderOneHour)
            offsets.Add(60);
        if (ReminderThirtyMinutes)
            offsets.Add(30);

        if (ReminderCustom)
        {
            var customOffset = (ReminderCustomHours * 60) + ReminderCustomMinutes;
            if (customOffset is < 1 or > 10080)
            {
                ErrorMessage = "Özel hatırlatıcı 1 dakika ile 7 gün arasında olmalıdır.";
                return;
            }

            offsets.Add(customOffset);
        }

        var input = new AppointmentInput(
            FirstName,
            LastName,
            Phone,
            startsAtLocal,
            SelectedDuration?.Minutes ?? 60,
            SelectedHost?.Id ?? string.Empty,
            SelectedCategory?.Id ?? string.Empty,
            SelectedMethod?.Value ?? AppointmentMethod.FaceToFace,
            Description,
            StudentFirstName,
            StudentLastName,
            offsets);

        IsBusy = true;
        try
        {
            var result = IsEdit
                ? await _appointments.UpdateAsync(_appointmentId!, input)
                : await _appointments.CreateAsync(input);

            if (!result.Success && result.Conflicts is { Count: > 0 })
            {
                if (!_dialogs.Confirm(result.Error ?? "Çakışma var. Yine de kaydedilsin mi?", "Çakışan görüşme"))
                    return;

                var forced = input with { IgnoreConflicts = true };
                result = IsEdit
                    ? await _appointments.UpdateAsync(_appointmentId!, forced)
                    : await _appointments.CreateAsync(forced);
            }

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Görüşme kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsDirty && !_dialogs.Confirm("Kaydedilmemiş değişiklikler var. Çıkılsın mı?", "Vazgeç"))
            return;

        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Cancelled;

    private bool IsDirty
        => !string.IsNullOrWhiteSpace(FirstName)
           || !string.IsNullOrWhiteSpace(LastName)
           || !string.IsNullOrWhiteSpace(Phone)
           || !string.IsNullOrWhiteSpace(Description)
           || !string.IsNullOrWhiteSpace(StudentFirstName)
           || !string.IsNullOrWhiteSpace(StudentLastName);

    private void EnsureTimeOption(string time)
    {
        if (string.IsNullOrWhiteSpace(time) || TimeOptions.Contains(time))
            return;

        TimeOptions.Add(time);
    }

    private void ApplyDefaultStart(DateTime initialDate)
    {
        var start = DefaultStart(initialDate);
        Date = start.Date;
        TimeText = start.ToString("HH:mm", CultureInfo.InvariantCulture);
        EnsureTimeOption(TimeText);
    }

    private DateTime DefaultStart(DateTime initialDate)
    {
        // Yeni görüşme: şimdiki saatten 1 saat sonrası (15 dk dilime yuvarlanır).
        var start = AppointmentDefaults.NextStart(
            DateTime.Now,
            _workHoursStart,
            _workHoursEnd,
            _allowOutOfHours);

        // Kullanıcı ileri bir tarihi görüntülüyorsa tarih korunur, saat yine 1 saat sonrasıdır.
        if (initialDate.Date > start.Date)
            return initialDate.Date + start.TimeOfDay;

        // Bugün veya geçmiş bir tarih görüntüleniyorsa hedef her zaman bugündür.
        return start;
    }

    private static TimeSpan ParseTime(string? text, TimeSpan fallback)
        => TimeSpan.TryParseExact(text?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        var value = text?.Trim() ?? string.Empty;
        if (TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time)
            || TimeSpan.TryParseExact(value, @"h\:mm", CultureInfo.InvariantCulture, out time))
        {
            return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }

        time = default;
        return false;
    }

    private bool CanSave() => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        EnterOutcomeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEnterOutcome));
    }

    partial void OnIsEditChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEnterOutcome));
        EnterOutcomeCommand.NotifyCanExecuteChanged();
    }
}
