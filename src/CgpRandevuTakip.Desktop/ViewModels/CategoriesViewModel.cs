using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private const string DefaultColor = "#2E7D5B";

    private readonly ICategoryService _categories;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;

    public CategoriesViewModel(
        ICategoryService categories,
        ICurrentUser currentUser,
        IDialogService dialogs)
    {
        _categories = categories;
        _currentUser = currentUser;
        _dialogs = dialogs;
    }

    public RangeObservableCollection<CategoryDto> Categories { get; } = new();

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _colorHex = DefaultColor;

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

    public string EditorTitle => IsEditing ? "Kategoriyi düzenle" : "Yeni kategori";

    public string SelectedSummary => SelectedCategory is null
        ? "Listeden bir kategori seçin."
        : $"{SelectedCategory.Name} · {SelectedCategory.UsageCount} görüşmede kullanılıyor";

    public string ToggleActiveLabel => SelectedCategory?.IsActive == true
        ? "Pasifleştir"
        : "Aktifleştir";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var selectedId = SelectedCategory?.Id;
        var items = await _categories.GetAllAsync();

        Categories.ReplaceWith(items);

        SelectedCategory = selectedId is null
            ? Categories.FirstOrDefault()
            : Categories.FirstOrDefault(x => x.Id == selectedId) ?? Categories.FirstOrDefault();

        StatusMessage = null;
    }

    [RelayCommand]
    private void NewCategory()
    {
        SelectedCategory = null;
        IsEditing = false;
        Name = string.Empty;
        ColorHex = DefaultColor;
        IsActive = true;
        ErrorMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EditCategory()
    {
        if (SelectedCategory is null)
            return;

        IsEditing = true;
        Name = SelectedCategory.Name;
        ColorHex = SelectedCategory.ColorHex;
        IsActive = SelectedCategory.IsActive;
        ErrorMessage = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var input = new CategoryInput(Name, ColorHex, IsActive);
            OperationResult result;

            if (IsEditing && SelectedCategory is not null)
                result = await _categories.UpdateAsync(SelectedCategory.Id, input);
            else
                result = await _categories.CreateAsync(input);

            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            var message = IsEditing ? "Kategori güncellendi." : "Kategori eklendi.";
            NewCategory();
            await LoadAsync();
            StatusMessage = message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kategori kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedCategory is null)
            return;

        if (!_dialogs.Confirm(
                $"'{SelectedCategory.Name}' kategorisi silinsin mi?\n\nKullanımda olan kategoriler silinemez; pasifleştirilebilir.",
                "Kategoriyi sil"))
            return;

        var result = await _categories.DeleteAsync(SelectedCategory.Id);
        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Kategori silinemedi.", "Kategori silinemedi");
            return;
        }

        await LoadAsync();
        StatusMessage = "Kategori silindi.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedCategory is null)
            return;

        var target = !SelectedCategory.IsActive;
        var result = await _categories.SetActiveAsync(SelectedCategory.Id, target);
        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Durum değiştirilemedi.", "Kategori");
            return;
        }

        await LoadAsync();
        StatusMessage = target ? "Kategori aktifleştirildi." : "Kategori pasifleştirildi.";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task MoveUpAsync() => await MoveAsync(-1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task MoveDownAsync() => await MoveAsync(1);

    private async Task MoveAsync(int direction)
    {
        if (SelectedCategory is null)
            return;

        var result = await _categories.MoveAsync(SelectedCategory.Id, direction);
        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Sıralama değiştirilemedi.", "Kategori");
            return;
        }

        await LoadAsync();
    }

    private bool HasSelection()
        => CanManage && SelectedCategory is not null && !IsBusy;

    private bool CanSave()
        => CanManage && !IsBusy;

    partial void OnSelectedCategoryChanged(CategoryDto? value)
    {
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(ToggleActiveLabel));
        NotifySelectionCommands();
    }

    partial void OnIsBusyChanged(bool value) => NotifySelectionCommands();

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(EditorTitle));

    private void NotifySelectionCommands()
    {
        EditCategoryCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }
}
