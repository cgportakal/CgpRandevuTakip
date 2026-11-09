using CgpRandevuTakip.Application.Recipients;
using CgpRandevuTakip.Desktop.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class RecipientsViewModel : ObservableObject
{
    private readonly IRecipientService _recipients;

    public RecipientsViewModel(IRecipientService recipients)
    {
        _recipients = recipients;
    }

    public RangeObservableCollection<RecipientDto> Recipients { get; } = new();

    [ObservableProperty]
    private RecipientDto? _selectedRecipient;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _tag = string.Empty;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public string EditorTitle => IsEditing ? "Alıcıyı düzenle" : "Yeni alıcı";

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var selectedId = SelectedRecipient?.Id;
        var rows = await _recipients.GetAllAsync();
        Recipients.ReplaceWith(rows);
        SelectedRecipient = selectedId is null
            ? Recipients.FirstOrDefault()
            : Recipients.FirstOrDefault(x => x.Id == selectedId) ?? Recipients.FirstOrDefault();
        StatusMessage = null;
    }

    [RelayCommand]
    private void New()
    {
        SelectedRecipient = null;
        IsEditing = false;
        Name = string.Empty;
        Phone = string.Empty;
        Tag = string.Empty;
        IsActive = true;
        ErrorMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand]
    private void EditSelected()
    {
        if (SelectedRecipient is null)
            return;

        IsEditing = true;
        Name = SelectedRecipient.Name;
        Phone = SelectedRecipient.PhoneE164;
        Tag = SelectedRecipient.Tag ?? string.Empty;
        IsActive = SelectedRecipient.IsActive;
        ErrorMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var input = new RecipientInput(Name, Phone, Tag, IsActive);
            var result = IsEditing && SelectedRecipient is not null
                ? await _recipients.UpdateAsync(SelectedRecipient.Id, input)
                : await _recipients.CreateAsync(input);

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            var message = IsEditing ? "Alıcı güncellendi." : "Alıcı eklendi.";
            New();
            await LoadAsync();
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Alıcı kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync()
    {
        if (SelectedRecipient is null)
            return;

        var result = await _recipients.UpdateAsync(
            SelectedRecipient.Id,
            new RecipientInput(
                SelectedRecipient.Name,
                SelectedRecipient.PhoneE164,
                SelectedRecipient.Tag,
                !SelectedRecipient.IsActive));

        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return;
        }

        await LoadAsync();
        StatusMessage = SelectedRecipient?.IsActive == true
            ? "Alıcı aktifleştirildi."
            : "Alıcı pasifleştirildi.";
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    partial void OnSelectedRecipientChanged(RecipientDto? value)
    {
        ToggleActiveLabel = value?.IsActive == true ? "Pasifleştir" : "Aktifleştir";
        OnPropertyChanged(nameof(ToggleActiveLabel));
    }

    public string ToggleActiveLabel { get; private set; } = "Pasifleştir";
}
