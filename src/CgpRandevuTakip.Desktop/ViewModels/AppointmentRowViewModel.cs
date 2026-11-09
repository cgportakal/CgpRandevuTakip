using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CgpRandevuTakip.Desktop.ViewModels;

/// <summary>
/// Günlük liste satırı. Seçim kutusu durumu UI'da tutulur; DTO değişmez kalır.
/// </summary>
public partial class AppointmentRowViewModel : ObservableObject
{
    public AppointmentRowViewModel(AppointmentListItemDto item)
    {
        Item = item;
    }

    public AppointmentListItemDto Item { get; }

    public string Id => Item.Id;

    public DateTime StartsAtLocal => Item.StartsAtLocal;

    public DateTime EndsAtLocal => Item.EndsAtLocal;

    public int DurationMinutes => Item.DurationMinutes;

    public string PersonFullName => Item.PersonFullName;

    public string PhoneE164 => Item.PhoneE164;

    public string HostName => Item.HostName;

    public string CategoryName => Item.CategoryName;

    public string CategoryColorHex => Item.CategoryColorHex;

    public AppointmentMethod Method => Item.Method;

    public string MethodText => Item.MethodText;

    public AppointmentStatus Status => Item.Status;

    public string StatusText => Item.StatusText;

    public bool IsOverdue => Item.IsOverdue;

    public string TimeLabel => Item.TimeLabel;

    public string EndTimeLabel => Item.EndTimeLabel;

    public string DateLabel => Item.DateLabel;

    [ObservableProperty]
    private bool _isSelected;

    public event EventHandler? SelectionChanged;

    partial void OnIsSelectedChanged(bool value)
        => SelectionChanged?.Invoke(this, EventArgs.Empty);
}
