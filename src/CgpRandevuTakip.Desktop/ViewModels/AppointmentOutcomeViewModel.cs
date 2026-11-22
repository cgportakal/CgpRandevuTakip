using System.Collections.ObjectModel;
using System.Globalization;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Services;
using CgpRandevuTakip.Domain.Rules;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record AttendanceOption(Attendance Value, string Label);

public partial class GuestOutcomeRowViewModel : ObservableObject
{
    public GuestOutcomeRowViewModel(
        string guestId,
        string name,
        Attendance? attendance,
        string? summary,
        bool willRepeat,
        string? postNotes,
        bool isClosed)
    {
        GuestId = guestId;
        Name = name;

        AttendanceOptions = new ObservableCollection<AttendanceOption>
        {
            new(Attendance.Arrived, "Geldi"),
            new(Attendance.NoShow, "Gelmedi"),
            new(Attendance.Postponed, "Ertelendi"),
            new(Attendance.Cancelled, "İptal")
        };

        _selectedAttendance = AttendanceOptions.FirstOrDefault(x => x.Value == attendance)
                              ?? AttendanceOptions[0];
        _summary = summary ?? string.Empty;
        _willRepeat = willRepeat;
        _postNotes = postNotes ?? string.Empty;
        _isClosed = isClosed;
    }

    public string GuestId { get; }

    public string Name { get; }

    public ObservableCollection<AttendanceOption> AttendanceOptions { get; }

    [ObservableProperty]
    private AttendanceOption? _selectedAttendance;

    [ObservableProperty]
    private string _summary;

    [ObservableProperty]
    private bool _willRepeat;

    [ObservableProperty]
    private string _postNotes;

    [ObservableProperty]
    private bool _isClosed;
}

public partial class AppointmentOutcomeViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly IAppointmentOutcomeService _outcomes;
    private readonly IDialogService _dialogs;

    private string _appointmentId = string.Empty;

    public AppointmentOutcomeViewModel(
        IAppointmentService appointments,
        IAppointmentOutcomeService outcomes,
        IDialogService dialogs)
    {
        _appointments = appointments;
        _outcomes = outcomes;
        _dialogs = dialogs;

        TimeOptions = new ObservableCollection<string>(
            AppointmentDefaults.BuildTimeOptions(
                AppointmentDefaults.DefaultDayStart,
                AppointmentDefaults.DefaultDayEnd,
                allowOutOfHours: false));

        AttendanceOptions = new ObservableCollection<AttendanceOption>
        {
            new(Attendance.Arrived, "Geldi"),
            new(Attendance.NoShow, "Gelmedi"),
            new(Attendance.Postponed, "Ertelendi"),
            new(Attendance.Cancelled, "İptal")
        };

        _selectedAttendance = AttendanceOptions[0];
    }

    public ObservableCollection<AttendanceOption> AttendanceOptions { get; }

    public ObservableCollection<string> TimeOptions { get; }

    public RangeObservableCollection<GuestOutcomeRowViewModel> GuestOutcomes { get; } = new();

    public bool HasMultipleGuests => GuestOutcomes.Count > 1;

    [ObservableProperty]
    private DateTime? _postponeDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private string _postponeTimeText = "09:00";

    public bool ShowPostponeFields
        => SelectedAttendance?.Value == Attendance.Postponed;

    [ObservableProperty]
    private string _appointmentSummary = string.Empty;

    [ObservableProperty]
    private AttendanceOption? _selectedAttendance;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _willRepeat;

    [ObservableProperty]
    private string _postNotes = string.Empty;

    [ObservableProperty]
    private bool _isClosed;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public string Title => "Görüşme sonucu";

    public bool IsClosedEditable
        => SelectedAttendance?.Value is Attendance.Arrived or Attendance.NoShow;

    public string ClosedHint => IsClosedEditable
        ? "Görüşme kapandıysa işaretleyin. 'Geldi' + kapandı seçimi durumu Tamamlandı yapar."
        : SelectedAttendance?.Value == Attendance.Cancelled
            ? "İptal edilen görüşme otomatik olarak kapanır."
            : "Ertelenen görüşme açık kalır; yeni tarih düzenleme ekranından verilir.";

    public event EventHandler? Saved;

    public event EventHandler? Cancelled;

    public async Task InitializeAsync(string appointmentId)
    {
        _appointmentId = appointmentId;
        IsBusy = true;
        try
        {
            var detail = await _appointments.GetDetailAsync(appointmentId);
            if (detail is null)
            {
                ErrorMessage = "Görüşme bulunamadı veya yetkiniz yok.";
                return;
            }

            AppointmentSummary =
                $"{detail.GuestSummaryText} · {detail.DateLabel} {detail.TimeRangeLabel} · {detail.HostNameWithTitle}"
                + (string.IsNullOrWhiteSpace(detail.RoomName) ? string.Empty : $" · {detail.RoomName}");

            PostponeDate = detail.StartsAtLocal.Date;
            PostponeTimeText = detail.StartsAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (!TimeOptions.Contains(PostponeTimeText))
                TimeOptions.Add(PostponeTimeText);

            var outcome = detail.Outcome;
            SelectedAttendance = AttendanceOptions.FirstOrDefault(x =>
                                    x.Value == (outcome?.Attendance ?? DefaultAttendance(detail.Status)))
                                ?? AttendanceOptions[0];

            Summary = outcome?.Summary ?? string.Empty;
            WillRepeat = outcome?.WillRepeat ?? false;
            PostNotes = outcome?.PostNotes ?? string.Empty;
            IsClosed = outcome?.IsClosed ?? detail.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled;

            GuestOutcomes.ReplaceWith((detail.Guests ?? Array.Empty<AppointmentGuestDto>())
                .Select(guest => new GuestOutcomeRowViewModel(
                    guest.GuestId,
                    guest.FullName,
                    guest.Outcome?.Attendance,
                    guest.Outcome?.Summary,
                    guest.Outcome?.WillRepeat ?? false,
                    guest.Outcome?.PostNotes,
                    guest.Outcome?.IsClosed ?? false)));
            OnPropertyChanged(nameof(HasMultipleGuests));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sonuç yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (SelectedAttendance is null)
        {
            ErrorMessage = "Katılım durumu seçilmelidir.";
            return;
        }

        DateTime? postponeStart = null;
        if (SelectedAttendance.Value == Attendance.Postponed)
        {
            if (PostponeDate is null)
            {
                ErrorMessage = "Ertelenen görüşme için yeni tarih seçilmelidir.";
                return;
            }

            if (!TimeSpan.TryParseExact(PostponeTimeText, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
            {
                ErrorMessage = "Yeni saat SS:dd biçiminde olmalıdır.";
                return;
            }

            postponeStart = PostponeDate.Value.Date + time;
            if (postponeStart < DateTime.Now)
            {
                ErrorMessage = "Geçmiş bir tarih/saat seçildi. Gelecek bir zaman seçin.";
                return;
            }

            if (!_dialogs.Confirm(
                    $"Görüşme {postponeStart:dd/MM/yyyy HH:mm} tarihine ertelenecek. Onaylıyor musunuz?",
                    "Erteleme onayı"))
                return;
        }

        IsBusy = true;
        try
        {
            IReadOnlyList<GuestOutcomeInput>? guestOutcomes = null;
            var aggregate = SelectedAttendance.Value;

            if (HasMultipleGuests)
            {
                guestOutcomes = GuestOutcomes
                    .Select(guest => new GuestOutcomeInput(
                        guest.GuestId,
                        guest.SelectedAttendance?.Value ?? Attendance.Arrived,
                        guest.Summary,
                        guest.WillRepeat,
                        guest.PostNotes,
                        guest.IsClosed))
                    .ToList();

                aggregate = AggregateAttendance(guestOutcomes);
            }

            var result = await _outcomes.SaveAsync(
                _appointmentId,
                new AppointmentOutcomeInput(
                    aggregate,
                    Summary,
                    WillRepeat,
                    PostNotes,
                    IsClosed,
                    guestOutcomes));

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Sonuç kaydedilemedi.";
                return;
            }

            if (result.Queued)
            {
                _dialogs.Info(
                    "Sunucuya şu anda ulaşılamıyor. Görüşme sonucu yerel kuyruğa alındı; bağlantı gelince gönderilecek.",
                    "Kuyruğa alındı");
            }

            if (postponeStart is not null)
            {
                var moved = await _appointments.MoveManyAsync(
                    new[] { _appointmentId },
                    postponeStart.Value.Date,
                    postponeStart.Value.TimeOfDay);

                if (moved.Error is not null)
                {
                    ErrorMessage = $"Sonuç kaydedildi ancak tarih güncellenemedi: {moved.Error}";
                    return;
                }

                if (moved.HasConflicts)
                {
                    if (!_dialogs.Confirm(
                            "Yeni tarihte çakışan bir görüşme var. Yine de ertelensin mi?",
                            "Çakışan görüşme"))
                    {
                        ErrorMessage = "Sonuç kaydedildi; tarih değiştirilmedi.";
                        return;
                    }

                    moved = await _appointments.MoveManyAsync(
                        moved.ConflictingIds,
                        postponeStart.Value.Date,
                        postponeStart.Value.TimeOfDay,
                        ignoreConflicts: true);
                }

                if (moved.Moved == 0)
                {
                    ErrorMessage = "Sonuç kaydedildi ancak görüşme taşınamadı.";
                    return;
                }
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sonuç kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private bool CanSave() => !IsBusy;

    partial void OnSelectedAttendanceChanged(AttendanceOption? value)
    {
        if (value?.Value == Attendance.Cancelled)
            IsClosed = true;
        else if (value?.Value == Attendance.Postponed)
            IsClosed = false;

        OnPropertyChanged(nameof(IsClosedEditable));
        OnPropertyChanged(nameof(ClosedHint));
        OnPropertyChanged(nameof(ShowPostponeFields));
    }

    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    private static Attendance AggregateAttendance(IReadOnlyList<GuestOutcomeInput> guests)
    {
        var values = guests.Select(x => x.Attendance).ToList();
        if (values.All(x => x == Attendance.NoShow))
            return Attendance.NoShow;
        if (values.All(x => x == Attendance.Cancelled))
            return Attendance.Cancelled;
        if (values.All(x => x == Attendance.Postponed))
            return Attendance.Postponed;
        return values.Contains(Attendance.Arrived) ? Attendance.Arrived : values[0];
    }

    private static Attendance DefaultAttendance(AppointmentStatus status) => status switch
    {
        AppointmentStatus.NoShow => Attendance.NoShow,
        AppointmentStatus.Postponed => Attendance.Postponed,
        AppointmentStatus.Cancelled => Attendance.Cancelled,
        _ => Attendance.Arrived
    };
}
