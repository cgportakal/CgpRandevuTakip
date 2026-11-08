using CgpRandevuTakip.Application.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public LoginViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public event EventHandler? LoginSucceeded;

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

    private bool CanLogin() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();

    partial void OnUsernameChanged(string value) => ErrorMessage = null;

    partial void OnPasswordChanged(string value) => ErrorMessage = null;
}
