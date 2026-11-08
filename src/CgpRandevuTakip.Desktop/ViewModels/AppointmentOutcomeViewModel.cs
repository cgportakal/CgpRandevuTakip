using System.Collections.ObjectModel;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record AttendanceOption(Attendance Value, string Label);

public partial class AppointmentOutcomeViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly IAppointmentOutcomeService _outcomes;

    private string _appointmentId = string.Empty;

    public AppointmentOutcomeViewModel(
        IAppointmentService appointments,
        IAppointmentOutcomeService outcomes)
    {
        _appointments = appointments;
        _outcomes = outcomes;

        AttendanceOptions = new ObservableCollection<AttendanceOption>
        {
            new(Attendance.Arrived, "Geldi"),
            new(Attendance.NoShow, "Gelmedi"),
            new(Attendance.Postponed, "Ertelendi"),
            new(Attendance.Cancelled, "İptal")
        };

        _selectedAttendance = AttendanceOptions[0];
    }

    public ObservableCollection<AttendanceOption> AttendanceOptions { get; }

    [ObservableProperty]
    private string _appointmentSummary = string.Empty;

    [ObservableProperty]
    private AttendanceOption? _selectedAttendance;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _willRepeat;

    [ObservableProperty]
    private string _postNotes = string.Empty;

    [ObservableProperty]
    private bool _isClosed;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public string Title => "Görüşme sonucu";

    public bool IsClosedEditable
        => SelectedAttendance?.Value is Attendance.Arrived or Attendance.NoShow;

    public string ClosedHint => IsClosedEditable
        ? "Görüşme kapandıysa işaretleyin. 'Geldi' + kapandı seçimi durumu Tamamlandı yapar."
        : SelectedAttendance?.Value == Attendance.Cancelled
            ? "İptal edilen görüşme otomatik olarak kapanır."
            : "Ertelenen görüşme açık kalır; yeni tarih düzenleme ekranından verilir.";

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public async Task InitializeAsync(string appointmentId)
    {
        _appointmentId = appointmentId;
        IsBusy = true;
        try
        {
            var detail = await _appointments.GetDetailAsync(appointmentId);
            if (detail is null)
            {
                ErrorMessage = "Görüşme bulunamadı veya yetkiniz yok.";
                return;
            }

            AppointmentSummary =
                $"{detail.PersonFullName} · {detail.DateLabel} {detail.TimeRangeLabel} · {detail.HostName}";

            var outcome = detail.Outcome;
            SelectedAttendance = AttendanceOptions.FirstOrDefault(x =>
                                    x.Value == (outcome?.Attendance ?? DefaultAttendance(detail.Status)))
                                ?? AttendanceOptions[0];

            Summary = outcome?.Summary ?? string.Empty;
            WillRepeat = outcome?.WillRepeat ?? false;
            PostNotes = outcome?.PostNotes ?? string.Empty;
            IsClosed = outcome?.IsClosed ?? detail.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sonuç yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedAttendance is null)
        {
            ErrorMessage = "Katılım durumu seçilmelidir.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _outcomes.SaveAsync(
                _appointmentId,
                new AppointmentOutcomeInput(
                    SelectedAttendance.Value,
                    Summary,
                    WillRepeat,
                    PostNotes,
                    IsClosed));

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Sonuç kaydedilemedi.";
                return;
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sonuç kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private bool CanSave() => !IsBusy;

    partial void OnSelectedAttendanceChanged(AttendanceOption? value)
    {
        if (value?.Value == Attendance.Cancelled)
            IsClosed = true;
        else if (value?.Value == Attendance.Postponed)
            IsClosed = false;

        OnPropertyChanged(nameof(IsClosedEditable));
        OnPropertyChanged(nameof(ClosedHint));
    }

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private static Attendance DefaultAttendance(AppointmentStatus status) => status switch
    {
        AppointmentStatus.NoShow => Attendance.NoShow,
        AppointmentStatus.Postponed => Attendance.Postponed,
        AppointmentStatus.Cancelled => Attendance.Cancelled,
        _ => Attendance.Arrived
    };
}
