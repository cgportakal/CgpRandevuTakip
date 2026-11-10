using System.Diagnostics;
using System.Windows;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Application.Reminders;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class ReminderActionViewModel : ObservableObject
{
    private readonly IReminderService _reminders;
    private readonly IClock _clock;
    private readonly IDialogService _dialogs;
    private readonly ReminderContextDto _context;

    public ReminderActionViewModel(
        IReminderService reminders,
        IClock clock,
        IDialogService dialogs,
        ReminderContextDto context)
    {
        _reminders = reminders;
        _clock = clock;
        _dialogs = dialogs;
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
    {
        var notify = _dialogs.Confirm(
            "Görüşme iptal edilecek. Kişiye WhatsApp ile bilgi verilsin mi?",
            "Ziyaretçi iptali");

        if (!await ApplyAsync(ReminderAction.VisitorCancelled))
            return;

        if (notify)
            OpenWhatsAppCancellation();
    }

    private async Task<bool> ApplyAsync(ReminderAction action)
    {
        if (IsBusy)
            return false;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _reminders.ApplyActionAsync(_context.ReminderId, action, _clock.UtcNow);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return false;
            }

            ActionTaken?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"İşlem yapılamadı: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenWhatsAppCancellation()
    {
        var message =
            $"Merhaba {PersonFullName}, {DateLabel} saat {TimeLabel} randevunuz iptal edilmiştir. " +
            "Bilgilendirme için teşekkür ederiz.";

        try
        {
            Clipboard.SetText(message);
        }
        catch
        {
            // Pano erişilemezse WhatsApp yine açılır.
        }

        var phone = new string(PhoneE164.Where(char.IsDigit).ToArray());
        var encoded = Uri.EscapeDataString(message);
        var deepLink = string.IsNullOrWhiteSpace(phone)
            ? "whatsapp://send"
            : $"whatsapp://send?phone={phone}&text={encoded}";

        try
        {
            Process.Start(new ProcessStartInfo(deepLink) { UseShellExecute = true });
        }
        catch
        {
            var webLink = string.IsNullOrWhiteSpace(phone)
                ? $"https://wa.me/?text={encoded}"
                : $"https://wa.me/{phone}?text={encoded}";

            try
            {
                Process.Start(new ProcessStartInfo(webLink) { UseShellExecute = true });
            }
            catch
            {
                // Tarayıcı/WhatsApp yoksa mesaj panoda kalır.
            }
        }
    }
}
