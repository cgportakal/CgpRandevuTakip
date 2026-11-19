using CgpRandevuTakip.Desktop.Services.Api;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>
/// Sunucu bağlantısı penceresi: adres, bağlantı testi, sunucu hesabıyla giriş/çıkış.
/// P1 aşamasında veri eşitleme yapmaz; yalnızca oturumu hazırlar.
/// </summary>
public partial class ServerConnectionViewModel : ObservableObject
{
    private readonly ApiConnectionService _connection;

    [ObservableProperty]
    private string _baseUrl = ApiSessionStore.DefaultBaseUrl;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionText = "Bağlı değil";

    public string Password { get; set; } = string.Empty;

    public ServerConnectionViewModel(ApiConnectionService connection)
    {
        _connection = connection;
    }

    public async Task InitializeAsync()
    {
        try
        {
            BaseUrl = await _connection.GetBaseUrlAsync();
            var session = await _connection.GetSessionAsync();
            if (session is not null)
            {
                IsConnected = true;
                ConnectionText = $"{session.User.DisplayName} · {session.User.Role}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        ClearMessages();
        IsBusy = true;
        try
        {
            var result = await _connection.TestAsync(BaseUrl);
            if (result.Success && result.Data is not null)
                StatusMessage = $"Sunucuya ulaşıldı · v{result.Data.Version}";
            else
                ErrorMessage = result.Error ?? "Sunucuya ulaşılamadı.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        ClearMessages();
        IsBusy = true;
        try
        {
            var result = await _connection.SignInAsync(BaseUrl, Username, Password);
            if (result.Success && result.Data is not null)
            {
                IsConnected = true;
                ConnectionText = $"{result.Data.DisplayName} · {result.Data.Role}";
                StatusMessage = "Sunucu oturumu açıldı. Veri eşitleme sonraki aşamada etkinleşecek.";
                Password = string.Empty;
            }
            else
            {
                ErrorMessage = result.Error ?? "Sunucuya giriş yapılamadı.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        ClearMessages();
        IsBusy = true;
        try
        {
            await _connection.SignOutAsync();
            IsConnected = false;
            ConnectionText = "Bağlı değil";
            StatusMessage = "Sunucu oturumu kapatıldı.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearMessages()
    {
        StatusMessage = null;
        ErrorMessage = null;
    }
}
