using CgpRandevuTakip.Application.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public SetupViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public event EventHandler? SetupSucceeded;

    public string ProductName => "CGP - Randevu Takip";

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var request = new SetupRequest(Username, DisplayName, Password, ConfirmPassword);
            var result = await _auth.CreateFirstAdminAsync(request);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            SetupSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kurulum tamamlanamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreate() => !IsBusy;

    partial void OnIsBusyChanged(bool value) => CreateCommand.NotifyCanExecuteChanged();

    partial void OnUsernameChanged(string value) => ErrorMessage = null;

    partial void OnDisplayNameChanged(string value) => ErrorMessage = null;

    partial void OnPasswordChanged(string value) => ErrorMessage = null;

    partial void OnConfirmPasswordChanged(string value) => ErrorMessage = null;
}
