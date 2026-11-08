using CgpRandevuTakip.Application.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class ForgotPasswordViewModel : ObservableObject
{
    private readonly IPasswordResetService _reset;

    public ForgotPasswordViewModel(IPasswordResetService reset)
    {
        _reset = reset;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isCodeSent;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public event EventHandler? Completed;
    public event EventHandler? CloseRequested;

    [RelayCommand]
    private async Task SendCodeAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _reset.RequestCodeAsync(Email);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            IsCodeSent = true;
            StatusMessage = "Kod gönderildi. Gelen kutunuzu ve spam klasörünü kontrol edin.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = await _reset.ResetAsync(Email, Code, NewPassword, ConfirmPassword);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            Code = string.Empty;
            IsCodeSent = false;
            StatusMessage = "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
            Completed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
