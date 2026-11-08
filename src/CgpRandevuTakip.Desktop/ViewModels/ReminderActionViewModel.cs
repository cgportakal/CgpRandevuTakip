using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Reminders;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class ReminderActionViewModel : ObservableObject
{
    private readonly IReminderService _reminders;
    private readonly IClock _clock;
    private readonly ReminderContextDto _context;

    public ReminderActionViewModel(
        IReminderService reminders,
        IClock clock,
        ReminderContextDto context)
    {
        _reminders = reminders;
        _clock = clock;
        _context = context;

        PersonFullName = context.PersonFullName;
        TimeLabel = context.TimeLabel;
        DateLabel = context.DateLabel;
        HostName = context.HostName;
        CategoryName = context.CategoryName;
        PhoneE164 = context.PhoneE164;
        MethodText = context.MethodText;
    }

    public string PersonFullName { get; }
    public string TimeLabel { get; }
    public string DateLabel { get; }
    public string HostName { get; }
    public string CategoryName { get; }
    public string PhoneE164 { get; }
    public string MethodText { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public event EventHandler? ActionTaken;

    [RelayCommand]
    private async Task DismissAsync()
        => await ApplyAsync(ReminderAction.DismissOnly);

    [RelayCommand]
    private async Task InformedAsync()
        => await ApplyAsync(ReminderAction.InformedHost);

    [RelayCommand]
    private async Task SnoozeAsync()
        => await ApplyAsync(ReminderAction.UnreachableSnooze);

    [RelayCommand]
    private async Task VisitorCancelledAsync()
        => await ApplyAsync(ReminderAction.VisitorCancelled);

    private async Task ApplyAsync(ReminderAction action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _reminders.ApplyActionAsync(_context.ReminderId, action, _clock.UtcNow);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            ActionTaken?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"İşlem yapılamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
