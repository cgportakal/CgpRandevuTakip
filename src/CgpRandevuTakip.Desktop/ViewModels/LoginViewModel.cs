using CgpRandevuTakip.Application.Auth;
using CgpRandevuTakip.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly IRememberedLoginStore _remembered;

    public LoginViewModel(
        IAuthService auth,
        IRememberedLoginStore remembered)
    {
        _auth = auth;
        _remembered = remembered;
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _rememberMe;

    public event EventHandler? LoginSucceeded;

    public event EventHandler? ForgotPasswordRequested;

    public string ProductName => "CGP - Randevu Takip";

    public string ProductSubtitle => "Eğitim kurumu randevu yönetimi";

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Kullanıcı adı zorunludur.";
            return;
        }

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

            if (RememberMe)
            {
                var token = await _auth.CreateRememberTokenAsync();
                if (!string.IsNullOrWhiteSpace(token))
                    _remembered.Save(token);
            }
            else
            {
                _remembered.Clear();
            }

            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Giriş yapılamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ForgotPassword()
        => ForgotPasswordRequested?.Invoke(this, EventArgs.Empty);

    private bool CanLogin() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    partial void OnUsernameChanged(string value) => ErrorMessage = null;

    partial void OnPasswordChanged(string value) => ErrorMessage = null;
}
