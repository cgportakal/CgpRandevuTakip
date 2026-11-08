using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class HostsViewModel : ObservableObject
{
    private readonly IHostService _hosts;
    private readonly Application.Abstractions.ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;

    public HostsViewModel(
        IHostService hosts,
        Application.Abstractions.ICurrentUser currentUser,
        IDialogService dialogs)
    {
        _hosts = hosts;
        _currentUser = currentUser;
        _dialogs = dialogs;
    }

    public RangeObservableCollection<HostDto> Hosts { get; } = new();

    [ObservableProperty]
    private HostDto? _selectedHost;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _loginEnabled;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool CanManage => _currentUser.IsInRole(UserRole.Admin);

    public bool ShowLoginFields => !IsEditing && LoginEnabled;

    public string EditorTitle => IsEditing ? "Görüşmeciyi düzenle" : "Yeni görüşmeci";

    public string ToggleActiveLabel => SelectedHost?.IsActive == true
        ? "Pasifleştir"
        : "Aktifleştir";

    public string SelectedSummary => SelectedHost is null
        ? "Listeden bir kişi seçin."
        : $"{SelectedHost.DisplayName} · {SelectedHost.AppointmentCount} görüşme · {RoleText(SelectedHost.Role)}";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var selectedId = SelectedHost?.Id;
        var rows = await _hosts.GetAllAsync();

        Hosts.ReplaceWith(rows);

        SelectedHost = selectedId is null
            ? Hosts.FirstOrDefault()
            : Hosts.FirstOrDefault(x => x.Id == selectedId) ?? Hosts.FirstOrDefault();

        StatusMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanManage))]
    private void NewHost()
    {
        SelectedHost = null;
        IsEditing = false;
        DisplayName = string.Empty;
        LoginEnabled = false;
        Username = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsActive = true;
        ErrorMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(ShowLoginFields));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EditSelected()
    {
        if (SelectedHost is null)
            return;

        IsEditing = true;
        DisplayName = SelectedHost.DisplayName;
        IsActive = SelectedHost.IsActive;
        LoginEnabled = SelectedHost.IsLoginEnabled;
        Username = SelectedHost.Username;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(ShowLoginFields));
    }

    [RelayCommand(CanExecute = nameof(CanManage))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var result = IsEditing && SelectedHost is not null
                ? await _hosts.UpdateAsync(SelectedHost.Id, new HostUpdateInput(DisplayName, IsActive))
                : await _hosts.CreateAsync(new HostCreateInput(
                    DisplayName,
                    LoginEnabled,
                    Username,
                    Password,
                    ConfirmPassword));

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            var message = IsEditing ? "Görüşmeci güncellendi." : "Görüşmeci eklendi.";
            NewHost();
            await LoadAsync();
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Görüşmeci kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedHost is null)
            return;

        var target = !SelectedHost.IsActive;
        var result = await _hosts.UpdateAsync(
            SelectedHost.Id,
            new HostUpdateInput(SelectedHost.DisplayName, target));

        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Durum değiştirilemedi.", "Görüşmeci");
            return;
        }

        await LoadAsync();
        StatusMessage = target ? "Görüşmeci aktifleştirildi." : "Görüşmeci pasifleştirildi.";
    }

    private bool HasSelection() => CanManage && SelectedHost is not null && !IsBusy;

    private static string RoleText(UserRole role) => role switch
    {
        UserRole.Admin => "Yönetici",
        UserRole.Secretary => "Sekreter",
        _ => "Görüşmeci"
    };

    partial void OnSelectedHostChanged(HostDto? value)
    {
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(ToggleActiveLabel));
        EditSelectedCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(ShowLoginFields));
    }

    partial void OnLoginEnabledChanged(bool value) => OnPropertyChanged(nameof(ShowLoginFields));

    partial void OnIsBusyChanged(bool value)
    {
        EditSelectedCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
    }
}
