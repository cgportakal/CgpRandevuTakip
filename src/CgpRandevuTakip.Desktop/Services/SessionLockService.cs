using System.Windows.Input;
using System.Windows.Threading;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Kullanıcı etkinliğini izler ve 30 dakika işlem yapılmazsa kilit isteği yayınlar.
/// Etkinlik, uygulamadaki tüm pencerelerden (modal diyaloglar dahil) izlenir.
/// Gerçek giriş/çıkış akışı MainWindow tarafından yönetilir.
/// </summary>
public sealed class SessionLockService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;
    private bool _started;

    public SessionLockService(IClock clock, IAuditLogger audit, ICurrentUser currentUser)
    {
        _clock = clock;
        _audit = audit;
        _currentUser = currentUser;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _timer.Tick += OnTimerTick;
    }

    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    public event EventHandler? LockRequested;

    public void Start()
    {
        LastActivityUtc = _clock.UtcNow;
        _started = true;
        _timer.Start();
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        InputManager.Current.PreProcessInput += OnPreProcessInput;
    }

    public void Stop()
    {
        _started = false;
        _timer.Stop();
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
    }

    public void NotifyActivity()
    {
        if (_started)
            LastActivityUtc = _clock.UtcNow;
    }

    private void OnPreProcessInput(object? sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs or TouchEventArgs)
            NotifyActivity();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_started)
            return;

        var now = _clock.UtcNow;
        if (!SessionLockPolicy.ShouldLock(LastActivityUtc, now))
            return;

        Stop();
        await _audit.LogAsync(
            AuditAction.SessionLocked,
            _currentUser.UserId,
            nameof(SessionLockService));

        LockRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Tick -= OnTimerTick;
        _timer.Stop();
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
    }
}
