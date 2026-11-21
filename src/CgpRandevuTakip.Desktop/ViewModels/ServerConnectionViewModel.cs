using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Desktop.Services.Api;
using CgpRandevuTakip.Desktop.Services.Sync;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>
/// Sunucu bağlantısı penceresi: adres, bağlantı testi, sunucu hesabıyla giriş/çıkış
/// ve P2 okuma modu anahtarı.
/// </summary>
public partial class ServerConnectionViewModel : ObservableObject
{
    private readonly ApiConnectionService _connection;
    private readonly ServerReadService _reads;
    private readonly OutboxService _outbox;
    private readonly DesktopRealtimeClient _realtime;
    private readonly IDialogService _dialogs;

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

    /// <summary>P2 okuma modu: günlük liste, detay ve raporlar sunucudan okunur.</summary>
    [ObservableProperty]
    private bool _readModeEnabled;

    [ObservableProperty]
    private string _outboxText = "Yazma kuyruğu boş.";

    [ObservableProperty]
    private bool _hasOutboxItems;

    [ObservableProperty]
    private bool _hasFailedOutbox;

    [ObservableProperty]
    private int _conflictCount;

    public bool HasConflicts => ConflictCount > 0;

    public string Password { get; set; } = string.Empty;

    public ServerConnectionViewModel(
        ApiConnectionService connection,
        ServerReadService reads,
        OutboxService outbox,
        DesktopRealtimeClient realtime,
        IDialogService dialogs)
    {
        _connection = connection;
        _reads = reads;
        _outbox = outbox;
        _realtime = realtime;
        _dialogs = dialogs;
    }

    public async Task InitializeAsync()
    {
        try
        {
            BaseUrl = await _connection.GetBaseUrlAsync();
            ReadModeEnabled = await _reads.IsReadModeEnabledAsync();

            var session = await _connection.GetSessionAsync();
            if (session is not null)
            {
                IsConnected = true;
                ConnectionText = $"{session.User.DisplayName} · {session.User.Role}";
            }

            await RefreshOutboxAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    partial void OnReadModeEnabledChanged(bool value)
        => _ = _reads.SetReadModeEnabledAsync(value);

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
                StatusMessage = "Sunucu oturumu açıldı. Okuma modunu açarak listeleri sunucudan okuyabilirsiniz.";
                Password = string.Empty;
                await _realtime.StartAsync();
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
            await _realtime.StopAsync();
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

    [RelayCommand]
    private async Task RetryOutboxAsync()
    {
        ClearMessages();
        IsBusy = true;
        try
        {
            var retried = await _outbox.RetryFailedAsync();
            var flush = await _outbox.FlushAsync();
            await RefreshOutboxAsync();

            if (flush.Sent > 0)
                StatusMessage = $"{flush.Sent} kuyruk kaydı gönderildi.";
            else if (retried > 0)
                StatusMessage = $"{retried} kayıt yeniden denemeye alındı.";
            else
                StatusMessage = "Gönderilecek kayıt yok.";
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

    private async Task RefreshOutboxAsync()
    {
        var pending = await _outbox.GetPendingCountAsync();
        var failed = await _outbox.GetFailedCountAsync();
        var conflicts = await _outbox.GetConflictCountAsync();
        ConflictCount = conflicts;

        HasOutboxItems = pending > 0 || failed > 0 || conflicts > 0;
        HasFailedOutbox = failed > 0;
        OutboxText = conflicts > 0
            ? $"{pending} bekleyen · {conflicts} çakışan kayıt"
            : failed > 0
                ? $"{pending} bekleyen · {failed} başarısız kayıt"
                : pending > 0
                    ? $"{pending} bekleyen kayıt"
                    : "Yazma kuyruğu boş.";
    }

    [RelayCommand]
    private async Task ResolveConflictsAsync()
    {
        ClearMessages();

        var conflicts = await _outbox.GetConflictsAsync();
        if (conflicts.Count == 0)
        {
            _dialogs.Info("Çakışan kuyruk kaydı yok.", "Kuyruk");
            return;
        }

        IsBusy = true;
        try
        {
            foreach (var item in conflicts)
            {
                var detail = await _reads.GetDetailAsync(item.EntityId);
                var server = detail.Success ? detail.Data : null;

                var message = server is null
                    ? "Sunucuda bu kayıt artık bulunmuyor veya erişilemiyor." +
                      Environment.NewLine + Environment.NewLine +
                      "Evet: yerel kaydı yeniden gönder" + Environment.NewLine +
                      "Hayır: kuyruk kaydını sil" + Environment.NewLine +
                      "İptal: sonra çöz"
                    : $"Çakışan kuyruk kaydı: {server.PersonFirstName} {server.PersonLastName}" +
                      Environment.NewLine +
                      $"Sunucu: {server.StartsAtLocal:dd.MM.yyyy HH:mm} · {server.HostName}" +
                      Environment.NewLine + Environment.NewLine +
                      "Evet: yerel değişikliği sunucuya uygula" + Environment.NewLine +
                      "Hayır: sunucudaki sürümü koru" + Environment.NewLine +
                      "İptal: sonra çöz";

                var choice = _dialogs.ResolveConflict(message, "Kuyruk çakışması");
                if (choice == ConflictResolution.Cancel)
                    break;

                await _outbox.ResolveConflictAsync(
                    item.Id,
                    choice == ConflictResolution.UseLocal,
                    server?.UpdatedAtUtc);
            }

            await RefreshOutboxAsync();
            var flush = await _outbox.FlushAsync();
            await RefreshOutboxAsync();

            StatusMessage = flush.Sent > 0
                ? $"{flush.Sent} çakışan kayıt gönderildi."
                : "Çakışma çözümü kaydedildi.";
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

    partial void OnConflictCountChanged(int value)
        => OnPropertyChanged(nameof(HasConflicts));
}
