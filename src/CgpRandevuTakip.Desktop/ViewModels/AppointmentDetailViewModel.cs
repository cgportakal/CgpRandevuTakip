using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class AppointmentDetailViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly IAppointmentOutcomeDialogService _outcomeDialogs;
    private readonly Application.Abstractions.ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;

    public AppointmentDetailViewModel(
        IAppointmentService appointments,
        IAppointmentOutcomeDialogService outcomeDialogs,
        Application.Abstractions.ICurrentUser currentUser,
        IDialogService dialogs)
    {
        _appointments = appointments;
        _outcomeDialogs = outcomeDialogs;
        _currentUser = currentUser;
        _dialogs = dialogs;
    }

    [ObservableProperty]
    private AppointmentDetailDto? _detail;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool NeedsRefresh { get; private set; }

    public bool CanEdit => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool CanDelete => _currentUser.IsInRole(UserRole.Admin);

    public bool CanEnterOutcome => Detail is not null && _currentUser.IsAuthenticated;

    public bool CanRepeat => CanEdit && Detail?.Outcome?.WillRepeat == true;

    public string OutcomeTitle => Detail?.Outcome is null
        ? "Henüz sonuç girilmedi"
        : $"{Detail.Outcome.AttendanceText} · {(Detail.Outcome.WillRepeat ? "Tekrar edilecek" : "Tekrar yok")}";

    public string OutcomeClosedText => Detail?.IsClosed == true
        ? $"Kapatıldı: {Detail.ClosedAtText} · {Detail.ClosedByNameText}"
        : "Görüşme açık";

    public string ReminderText
    {
        get
        {
            if (Detail is null || Detail.ReminderOffsetsMinutes.Count == 0)
                return "Hatırlatıcı yok";

            return string.Join(" · ", Detail.ReminderOffsetsMinutes.Select(FormatReminder));
        }
    }

    public string DescriptionText => string.IsNullOrWhiteSpace(Detail?.Description)
        ? "—"
        : Detail.Description;

    public string StudentText => string.IsNullOrWhiteSpace(Detail?.StudentFullName)
        ? "—"
        : Detail.StudentFullName;

    public event EventHandler? EditRequested;

    public event EventHandler? Deleted;
    public event EventHandler? CloseRequested;
    public event EventHandler? RepeatRequested;

    public async Task LoadAsync(string appointmentId)
    {
        IsBusy = true;
        try
        {
            Detail = await _appointments.GetDetailAsync(appointmentId);
            if (Detail is null)
                ErrorMessage = "Görüşme bulunamadı veya görüntüleme yetkiniz yok.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Görüşme yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEnterOutcome))]
    private async Task EnterOutcomeAsync()
    {
        if (Detail is null)
            return;

        if (await _outcomeDialogs.ShowAsync(Detail.Id))
        {
            NeedsRefresh = true;
            await LoadAsync(Detail.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRepeat))]
    private void Repeat() => RepeatRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit() => EditRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (Detail is null)
            return;

        if (!_dialogs.Confirm(
                $"{Detail.PersonFullName} · {Detail.DateLabel} {Detail.TimeRangeLabel}\n\nGörüşme silinsin mi? 30 gün içinde yönetici tarafından geri alınabilir.",
                "Görüşmeyi sil"))
            return;

        var result = await _appointments.DeleteAsync(Detail.Id);
        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Görüşme silinemedi.", "Görüşme silinemedi");
            return;
        }

        NeedsRefresh = true;
        Deleted?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    partial void OnDetailChanged(AppointmentDetailDto? value)
    {
        OnPropertyChanged(nameof(ReminderText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(StudentText));
        OnPropertyChanged(nameof(CanEnterOutcome));
        OnPropertyChanged(nameof(CanRepeat));
        OnPropertyChanged(nameof(OutcomeTitle));
        OnPropertyChanged(nameof(OutcomeClosedText));
        EnterOutcomeCommand.NotifyCanExecuteChanged();
        RepeatCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
    }

    private static string FormatReminder(int minutes) => minutes switch
    {
        1440 => "1 gün önce",
        60 => "1 saat önce",
        30 => "30 dakika önce",
        _ => $"{minutes} dakika önce"
    };
}
