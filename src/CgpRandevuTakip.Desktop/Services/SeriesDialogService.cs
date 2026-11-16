using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Desktop.ViewModels;
using CgpRandevuTakip.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>Seri yönetimi (kayıt ekle / düzenle) penceresini açar.</summary>
public interface ISeriesDialogService
{
    Task<bool> ShowManageAsync(AppointmentDetailDto detail);
}

public sealed class SeriesDialogService : ISeriesDialogService
{
    private readonly IServiceProvider _services;

    public SeriesDialogService(IServiceProvider services) => _services = services;

    public async Task<bool> ShowManageAsync(AppointmentDetailDto detail)
    {
        var window = _services.GetRequiredService<SeriesManageWindow>();
        window.Owner = Owner;
        await window.InitializeAsync(detail);
        return window.ShowDialog() == true;
    }

    private static System.Windows.Window? Owner
        => System.Windows.Application.Current?.MainWindow is { IsVisible: true } window
            ? window
            : null;
}
