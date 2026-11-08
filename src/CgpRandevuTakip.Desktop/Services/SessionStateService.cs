namespace CgpRandevuTakip.Desktop.Services;

/// <summary>
/// Oturum kilidini bildirim katmanına taşır. Kilitliyken PII'li aksiyon penceresi
/// açılmaz; yalnızca PII'siz bildirim gösterilir.
/// </summary>
public sealed class SessionStateService
{
    private bool _isLocked;

    public bool IsLocked => _isLocked;

    public event EventHandler? LockStateChanged;

    public void SetLocked(bool value)
    {
        if (_isLocked == value)
            return;

        _isLocked = value;
        LockStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
