using System.Collections.ObjectModel;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record RoleOption(UserRole Value, string Label);

public partial class UsersViewModel : ObservableObject
{
    private readonly IUserService _users;
    private readonly Application.Abstractions.ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;

    public UsersViewModel(
        IUserService users,
        Application.Abstractions.ICurrentUser currentUser,
        IDialogService dialogs)
    {
        _users = users;
        _currentUser = currentUser;
        _dialogs = dialogs;

        RoleOptions = new ObservableCollection<RoleOption>
        {
            new(UserRole.Admin, "Yönetici"),
            new(UserRole.Secretary, "Sekreter"),
            new(UserRole.Host, "Görüşmeci")
        };

        _selectedRole = RoleOptions[2];
    }

    public RangeObservableCollection<UserDto> Users { get; } = new();

    public ObservableCollection<RoleOption> RoleOptions { get; }

    [ObservableProperty]
    private UserDto? _selectedUser;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private RoleOption? _selectedRole;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isLoginEnabled = true;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _resetPassword = string.Empty;

    [ObservableProperty]
    private string _resetConfirmPassword = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    public bool CanManage => _currentUser.IsInRole(UserRole.Admin);

    public bool ShowNewPasswordFields => !IsEditing && IsLoginEnabled;

    public bool ShowResetPasswordFields => IsEditing && SelectedUser is not null;

    public bool IsLoginToggleEnabled => SelectedRole?.Value == UserRole.Host;

    public string EditorTitle => IsEditing ? "Kullanıcıyı düzenle" : "Yeni kullanıcı";

    public string SelectedSummary => SelectedUser is null
        ? "Listeden bir kullanıcı seçin."
        : $"{SelectedUser.DisplayName} · {SelectedUser.Username} · {SelectedUser.RoleText}";

    public string ToggleActiveLabel => SelectedUser?.IsActive == true
        ? "Pasifleştir"
        : "Aktifleştir";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var selectedId = SelectedUser?.Id;
        var rows = await _users.GetAllAsync();

        Users.ReplaceWith(rows);

        SelectedUser = selectedId is null
            ? Users.FirstOrDefault()
            : Users.FirstOrDefault(x => x.Id == selectedId) ?? Users.FirstOrDefault();

        StatusMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanManage))]
    private void NewUser()
    {
        SelectedUser = null;
        IsEditing = false;
        DisplayName = string.Empty;
        Username = string.Empty;
        Email = string.Empty;
        SelectedRole = RoleOptions.First(x => x.Value == UserRole.Host);
        IsActive = true;
        IsLoginEnabled = true;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ResetPassword = string.Empty;
        ResetConfirmPassword = string.Empty;
        ErrorMessage = null;
        StatusMessage = null;
        NotifyEditorState();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EditSelected()
    {
        if (SelectedUser is null)
            return;

        IsEditing = true;
        DisplayName = SelectedUser.DisplayName;
        Username = SelectedUser.Username;
        Email = SelectedUser.Email ?? string.Empty;
        SelectedRole = RoleOptions.FirstOrDefault(x => x.Value == SelectedUser.Role) ?? RoleOptions[2];
        IsActive = SelectedUser.IsActive;
        IsLoginEnabled = SelectedUser.IsLoginEnabled;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ResetPassword = string.Empty;
        ResetConfirmPassword = string.Empty;
        ErrorMessage = null;
        StatusMessage = null;
        NotifyEditorState();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var role = SelectedRole?.Value ?? UserRole.Host;
            var result = IsEditing && SelectedUser is not null
                ? await _users.UpdateAsync(
                    SelectedUser.Id,
                    new UserUpdateInput(DisplayName, role, IsActive, IsLoginEnabled, Email))
                : await _users.CreateAsync(new UserCreateInput(
                    Username,
                    DisplayName,
                    role,
                    IsLoginEnabled,
                    Password,
                    ConfirmPassword,
                    Email));

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            var message = IsEditing ? "Kullanıcı güncellendi." : "Kullanıcı eklendi.";
            NewUser();
            await LoadAsync();
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kullanıcı kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetPassword))]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is null)
            return;

        ErrorMessage = null;
        StatusMessage = null;

        var result = await _users.ResetPasswordAsync(
            SelectedUser.Id,
            new UserResetPasswordInput(ResetPassword, ResetConfirmPassword));

        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return;
        }

        ResetPassword = string.Empty;
        ResetConfirmPassword = string.Empty;
        StatusMessage = "Şifre güncellendi. Kullanıcı yeni şifresiyle giriş yapabilir.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedUser is null)
            return;

        var target = !SelectedUser.IsActive;
        var result = await _users.UpdateAsync(
            SelectedUser.Id,
            new UserUpdateInput(
                SelectedUser.DisplayName,
                SelectedUser.Role,
                target,
                SelectedUser.IsLoginEnabled,
                SelectedUser.Email));

        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Durum değiştirilemedi.", "Kullanıcı");
            return;
        }

        await LoadAsync();
        StatusMessage = target ? "Kullanıcı aktifleştirildi." : "Kullanıcı pasifleştirildi.";
    }

    private bool HasSelection() => CanManage && SelectedUser is not null && !IsBusy;

    private bool CanSave() => CanManage && !IsBusy;

    private bool CanResetPassword() => CanManage && SelectedUser is not null && !IsBusy;

    partial void OnSelectedUserChanged(UserDto? value)
    {
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(ToggleActiveLabel));
        OnPropertyChanged(nameof(ShowResetPasswordFields));
        EditSelectedCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        ResetPasswordCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsEditingChanged(bool value) => NotifyEditorState();

    partial void OnSelectedRoleChanged(RoleOption? value)
    {
        if (value is not null && value.Value != UserRole.Host && !IsLoginEnabled)
            IsLoginEnabled = true;

        OnPropertyChanged(nameof(IsLoginToggleEnabled));
        NotifyEditorState();
    }

    partial void OnIsLoginEnabledChanged(bool value) => NotifyEditorState();

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        EditSelectedCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        ResetPasswordCommand.NotifyCanExecuteChanged();
    }

    private void NotifyEditorState()
    {
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(ShowNewPasswordFields));
        OnPropertyChanged(nameof(ShowResetPasswordFields));
    }
}
