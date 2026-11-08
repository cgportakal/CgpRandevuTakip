using CgpRandevuTakip.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.Services;

public interface IAppointmentOutcomeDialogService
{
    Task<bool> ShowAsync(string appointmentId);
}

public sealed class AppointmentOutcomeDialogService : IAppointmentOutcomeDialogService
{
    private readonly IServiceProvider _services;

    public AppointmentOutcomeDialogService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> ShowAsync(string appointmentId)
    {
        var window = _services.GetRequiredService<AppointmentOutcomeWindow>();
        window.Owner = Owner;
        await window.InitializeAsync(appointmentId);
        return window.ShowDialog() == true;
    }

    private static System.Windows.Window? Owner
        => System.Windows.Application.Current?.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsVisible && w.IsActive)
           ?? System.Windows.Application.Current?.MainWindow;
}
