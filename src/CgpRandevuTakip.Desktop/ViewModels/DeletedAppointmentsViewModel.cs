using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class DeletedAppointmentsViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly IDialogService _dialogs;

    public DeletedAppointmentsViewModel(
        IAppointmentService appointments,
        IDialogService dialogs)
    {
        _appointments = appointments;
        _dialogs = dialogs;
    }

    public RangeObservableCollection<DeletedAppointmentDto> Appointments { get; } = new();

    [ObservableProperty]
    private DeletedAppointmentDto? _selectedAppointment;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasAppointments;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public event EventHandler? Restored;

    public event EventHandler? CloseRequested;

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await _appointments.GetDeletedAsync();
            Appointments.ReplaceWith(rows);
            HasAppointments = rows.Count > 0;

            if (SelectedAppointment is not null
                && rows.All(x => x.Id != SelectedAppointment.Id))
            {
                SelectedAppointment = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silinen görüşmeler yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RestoreAsync()
    {
        if (SelectedAppointment is null)
            return;

        if (!_dialogs.Confirm(
                $"{SelectedAppointment.PersonFullName} · {SelectedAppointment.DateLabel} {SelectedAppointment.TimeLabel}\n\nGörüşme geri alınsın mı? Gelecekteki hatırlatıcılar yeniden planlanır.",
                "Görüşmeyi geri al"))
            return;

        ErrorMessage = null;
        StatusMessage = null;
        var result = await _appointments.RestoreAsync(SelectedAppointment.Id);
        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Görüşme geri alınamadı.";
            return;
        }

        StatusMessage = "Görüşme geri alındı.";
        Restored?.Invoke(this, EventArgs.Empty);
        await LoadAsync();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private bool HasSelection() => SelectedAppointment is not null && !IsBusy;

    partial void OnSelectedAppointmentChanged(DeletedAppointmentDto? value)
        => RestoreCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
        => RestoreCommand.NotifyCanExecuteChanged();
}
