using CgpRandevuTakip.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.Services;

public interface IAppointmentDialogService
{
    Task<bool> ShowCreateAsync(DateTime initialDate);
    Task<bool> ShowEditAsync(string appointmentId);
    Task<bool> ShowDetailAsync(string appointmentId);
    Task<bool> ShowRepeatAsync(string sourceAppointmentId, DateTime initialDate);
    Task<bool> ShowDeletedAsync();
}

public sealed class AppointmentDialogService : IAppointmentDialogService
{
    private readonly IServiceProvider _services;

    public AppointmentDialogService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> ShowCreateAsync(DateTime initialDate)
    {
        var window = _services.GetRequiredService<AppointmentEditorWindow>();
        window.Owner = Owner;
        await window.InitializeAsync(null, initialDate);
        return window.ShowDialog() == true;
    }

    public async Task<bool> ShowEditAsync(string appointmentId)
    {
        var window = _services.GetRequiredService<AppointmentEditorWindow>();
        window.Owner = Owner;
        await window.InitializeAsync(appointmentId, DateTime.Today);
        return window.ShowDialog() == true;
    }

    public async Task<bool> ShowRepeatAsync(string sourceAppointmentId, DateTime initialDate)
    {
        var window = _services.GetRequiredService<AppointmentEditorWindow>();
        window.Owner = Owner;
        await window.InitializeRepeatAsync(sourceAppointmentId, initialDate);
        return window.ShowDialog() == true;
    }

    public async Task<bool> ShowDeletedAsync()
    {
        var window = _services.GetRequiredService<DeletedAppointmentsWindow>();
        window.Owner = Owner;
        await window.InitializeAsync();
        window.ShowDialog();
        return window.RestoredAny;
    }

    public async Task<bool> ShowDetailAsync(string appointmentId)
    {
        while (true)
        {
            var window = _services.GetRequiredService<AppointmentDetailWindow>();
            window.Owner = Owner;
            await window.InitializeAsync(appointmentId);
            window.ShowDialog();

            if (window.EditRequested)
            {
                if (await ShowEditAsync(appointmentId))
                    return true;

                continue;
            }

            if (window.RepeatRequested)
                return await ShowRepeatAsync(appointmentId, DateTime.Today.AddDays(7));

            return window.NeedsRefresh;
        }
    }

    private static System.Windows.Window? Owner
        => System.Windows.Application.Current?.MainWindow is { IsVisible: true } window
            ? window
            : null;
}
