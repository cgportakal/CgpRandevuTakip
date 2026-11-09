using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>Toplu taşıma penceresinin seçim sonucu.</summary>
public sealed record BulkMoveSelection(DateTime NewDate, TimeSpan? NewTime);

public partial class BulkMoveViewModel : ObservableObject
{
    private IReadOnlyList<DateTime> _startsAtLocal = Array.Empty<DateTime>();

    public BulkMoveViewModel()
    {
        // 06:00–20:45 arası 15 dakikalık seçenekler.
        TimeOptions = new ObservableCollection<string>(
            Enumerable.Range(24, 60)
                .Select(i => TimeSpan.FromMinutes(i * 15).ToString(@"hh\:mm", CultureInfo.InvariantCulture)));
    }

    public ObservableCollection<string> TimeOptions { get; }

    [ObservableProperty]
    private string _title = "Görüşmeleri taşı";

    [ObservableProperty]
    private string _countText = string.Empty;

    [ObservableProperty]
    private DateTime? _newDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private bool _keepTimes = true;

    [ObservableProperty]
    private string _newTimeText = "09:00";

    [ObservableProperty]
    private string? _errorMessage;

    public BulkMoveSelection? Selection { get; private set; }

    public event EventHandler? Completed;

    public bool UseNewTime
    {
        get => !KeepTimes;
        set => KeepTimes = !value;
    }

    public void Initialize(int count, DateTime initialDate, IReadOnlyList<DateTime> startsAtLocal)
    {
        _startsAtLocal = startsAtLocal;
        Title = count == 1 ? "Görüşmeyi taşı" : $"{count} görüşmeyi taşı";
        CountText = count == 1
            ? "1 görüşme yeni tarihe taşınacak."
            : $"{count} görüşme yeni tarihe taşınacak.";
        NewDate = initialDate.Date;

        var firstTime = startsAtLocal.Count > 0
            ? startsAtLocal[0].ToString("HH:mm", CultureInfo.InvariantCulture)
            : "09:00";

        if (!TimeOptions.Contains(firstTime))
            TimeOptions.Add(firstTime);

        NewTimeText = firstTime;
        KeepTimes = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void Move()
    {
        ErrorMessage = null;

        if (NewDate is null)
        {
            ErrorMessage = "Yeni tarih seçilmelidir.";
            return;
        }

        var date = NewDate.Value.Date;
        TimeSpan? newTime = null;

        if (UseNewTime)
        {
            if (!TryParseTime(NewTimeText, out var parsed))
            {
                ErrorMessage = "Saat SS:dd biçiminde olmalıdır (ör. 14:30).";
                return;
            }

            newTime = parsed;
        }

        var now = DateTime.Now;
        if (date < now.Date)
        {
            ErrorMessage = "Geçmiş bir tarihe taşıma yapılamaz.";
            return;
        }

        if (newTime is not null && date == now.Date && date + newTime.Value < now)
        {
            ErrorMessage = "Seçilen saat geçmişte. Gelecek bir saat seçin.";
            return;
        }

        Selection = new BulkMoveSelection(date, newTime);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Cancelled;

    partial void OnKeepTimesChanged(bool value)
        => OnPropertyChanged(nameof(UseNewTime));

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        var value = text?.Trim() ?? string.Empty;
        return TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time)
               || TimeSpan.TryParseExact(value, @"h\:mm", CultureInfo.InvariantCulture, out time);
    }
}
