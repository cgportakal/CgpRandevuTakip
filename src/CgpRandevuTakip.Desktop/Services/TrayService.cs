using System.Drawing;
using System.Windows.Forms;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Sistem tepsisindeki simgeyi ve menüsünü yönetir. Pencere kapatıldığında uygulama
/// tepside çalışmaya devam eder.
/// </summary>
public sealed class TrayService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private System.Windows.Window? _mainWindow;

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    public bool IsExiting { get; private set; }

    public bool HasMainWindow => _mainWindow is not null;

    public void Initialize()
    {
        if (_notifyIcon is not null)
            return;

        var menu = new ContextMenuStrip();
        var open = menu.Items.Add("Aç");
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var exit = menu.Items.Add("Çıkış");
        exit.Click += (_, _) => RequestExit();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "CGP - Randevu Takip",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AttachWindow(System.Windows.Window window) => _mainWindow = window;

    public void ShowWindow()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.Show();
        if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
            _mainWindow.WindowState = System.Windows.WindowState.Normal;

        _mainWindow.Activate();
    }

    public void ShowBalloon(string title, string text)
    {
        if (_notifyIcon is null)
            return;

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void RequestExit()
    {
        if (IsExiting)
            return;

        IsExiting = true;
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
            return;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }
}
