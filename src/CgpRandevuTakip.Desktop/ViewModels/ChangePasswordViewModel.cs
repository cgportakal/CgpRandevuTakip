using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IRememberedLoginStore _remembered;
    private readonly ICurrentUser _currentUser;

    public ChangePasswordViewModel(
        IAuthService auth,
        IRememberedLoginStore remembered,
        ICurrentUser currentUser)
    {
        _auth = auth;
        _remembered = remembered;
        _currentUser = currentUser;
    }

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isBusy;

    public event EventHandler? Completed;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;
        IsBusy = true;
        try
        {
            var result = await _auth.ChangePasswordAsync(CurrentPassword, NewPassword, ConfirmPassword);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            SuccessMessage = "Şifreniz güncellendi.";

            // "Beni hatırla" kayıtlı kimliği eski şifreyle kalmasın.
            if (_remembered.LoadCredentials() is not null
                && !string.IsNullOrWhiteSpace(_currentUser.Username))
            {
                _remembered.SaveCredentials(_currentUser.Username, NewPassword);
            }

            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Şifre değiştirilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSave() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    partial void OnCurrentPasswordChanged(string value) => ErrorMessage = null;

    partial void OnNewPasswordChanged(string value) => ErrorMessage = null;

    partial void OnConfirmPasswordChanged(string value) => ErrorMessage = null;
}
