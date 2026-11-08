using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class LockViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _currentUser;

    public LockViewModel(IAuthService auth, IAuditLogger audit, ICurrentUser currentUser)
    {
        _auth = auth;
        _audit = audit;
        _currentUser = currentUser;
        _username = currentUser.Username;
        _displayName = currentUser.DisplayName;
    }

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public string DisplayName => _displayName;

    private readonly string _displayName;

    public string Message => "Oturumunuz 30 dakika işlem yapılmadığı için kilitlendi.";

    public event EventHandler? Unlocked;

    public event EventHandler? LogoutRequested;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "Şifre zorunludur.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Username, Password);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                Password = string.Empty;
                return;
            }

            Password = string.Empty;
            await _audit.LogAsync(
                AuditAction.SessionUnlocked,
                _currentUser.UserId,
                nameof(LockViewModel));

            Unlocked?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Oturum açılamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Logout() => LogoutRequested?.Invoke(this, EventArgs.Empty);

    private bool CanUnlock() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => UnlockCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => ErrorMessage = null;
}
