using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using CgpRandevuTakip.Application.Recipients;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public partial class RecipientSelection : ObservableObject
{
    public RecipientSelection(RecipientDto recipient)
    {
        Id = recipient.Id;
        Name = recipient.Name;
        PhoneE164 = recipient.PhoneE164;
        Tag = recipient.Tag;
    }

    public string Id { get; }
    public string Name { get; }
    public string PhoneE164 { get; }
    public string? Tag { get; }
    public string DisplayText => string.IsNullOrWhiteSpace(Tag)
        ? $"{Name} · {PhoneE164}"
        : $"{Name} · {PhoneE164} · {Tag}";

    [ObservableProperty]
    private bool _isSelected;
}

public partial class ShareDailyListViewModel : ObservableObject
{
    private readonly IDailyShareService _share;
    private readonly IRecipientService _recipients;
    private readonly IDialogService _dialogs;

    private DailySharePreview? _preview;

    public ShareDailyListViewModel(
        IDailyShareService share,
        IRecipientService recipients,
        IDialogService dialogs)
    {
        _share = share;
        _recipients = recipients;
        _dialogs = dialogs;

        Recipients.CollectionChanged += (_, _) => HookRecipients();
    }

    public RangeObservableCollection<RecipientSelection> Recipients { get; } = new();

    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private int _rowCount;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public string DateLabel => Date.ToString("dd/MM/yyyy");

    public string SelectedCountText
    {
        get
        {
            var count = Recipients.Count(x => x.IsSelected);
            return count == 0 ? "Alıcı seçilmedi" : $"{count} alıcı seçildi";
        }
    }

    public event EventHandler? RecipientsRequested;
    public event EventHandler? CloseRequested;

    public async Task LoadAsync(DateTime date)
    {
        Date = date.Date;
        OnPropertyChanged(nameof(DateLabel));
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            _preview = await _share.BuildAsync(date);
            PreviewImage = _preview.Image;
            Message = _preview.Message;
            SummaryText = _preview.Summary;
            RowCount = _preview.Rows.Count;

            var recipients = await _recipients.GetActiveAsync();
            Recipients.ReplaceWith(recipients.Select(x => new RecipientSelection(x)));
            OnPropertyChanged(nameof(SelectedCountText));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Liste hazırlanamadı: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PrepareAndOpenAsync()
    {
        if (_preview is null)
            return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var selected = Recipients.Where(x => x.IsSelected).Select(x => x.Id).ToList();
            var result = await _share.PrepareAndOpenAsync(
                _preview, selected, openWhatsApp: true, Message);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            StatusMessage =
                "Görsel panoya kopyalandı. WhatsApp'ta sohbete gidip Ctrl+V ile yapıştırın.";
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyOnlyAsync()
    {
        if (_preview is null)
            return;

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var selected = Recipients.Where(x => x.IsSelected).Select(x => x.Id).ToList();
            var result = await _share.PrepareAndOpenAsync(
                _preview, selected, openWhatsApp: false, Message);
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            StatusMessage = "Görsel panoya kopyalandı.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenRecipients() => RecipientsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void HookRecipients()
    {
        foreach (var recipient in Recipients)
            recipient.PropertyChanged += OnRecipientChanged;
    }

    private void OnRecipientChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecipientSelection.IsSelected))
            OnPropertyChanged(nameof(SelectedCountText));
    }
}
