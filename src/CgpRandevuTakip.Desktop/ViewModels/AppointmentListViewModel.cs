using System.Windows.Threading;
using CgpRandevuTakip.Application.Appointments;
using CgpRandevuTakip.Application.Categories;
using CgpRandevuTakip.Application.Hosts;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Desktop.Messages;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record CategoryFilterOption(string? Id, string Label);

public sealed record StatusFilterOption(AppointmentStatus? Value, string Label);

public sealed record HostFilterOption(string? Id, string Label);

public partial class AppointmentListViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly ICategoryService _categories;
    private readonly IHostService _hosts;
    private readonly Application.Abstractions.ICurrentUser _currentUser;
    private readonly Services.IDialogService _dialogs;
    private readonly Services.IAppointmentDialogService _appointmentDialogs;
    private readonly DispatcherTimer _searchTimer;

    private bool _filtersLoaded;
    private bool _suppressReload;

    public AppointmentListViewModel(
        IAppointmentService appointments,
        ICategoryService categories,
        IHostService hosts,
        Application.Abstractions.ICurrentUser currentUser,
        Services.IDialogService dialogs,
        Services.IAppointmentDialogService appointmentDialogs)
    {
        _appointments = appointments;
        _categories = categories;
        _hosts = hosts;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _appointmentDialogs = appointmentDialogs;

        CategoryFilters = new RangeObservableCollection<CategoryFilterOption>
        {
            new(null, "Tüm kategoriler")
        };

        StatusFilters = new RangeObservableCollection<StatusFilterOption>
        {
            new(null, "Tüm durumlar"),
            new(AppointmentStatus.Scheduled, "Planlandı"),
            new(AppointmentStatus.Arrived, "Geldi"),
            new(AppointmentStatus.NoShow, "Gelmedi"),
            new(AppointmentStatus.Postponed, "Ertelendi"),
            new(AppointmentStatus.Cancelled, "İptal"),
            new(AppointmentStatus.Completed, "Tamamlandı")
        };

        _selectedCategoryFilter = CategoryFilters[0];
        _selectedStatusFilter = StatusFilters[0];

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await LoadAsync();
        };

        WeakReferenceMessenger.Default.Register<OpenDateMessage>(
            this,
            static (recipient, message) =>
                ((AppointmentListViewModel)recipient).ApplyDate(message.Date));
    }

    public RangeObservableCollection<AppointmentListItemDto> Appointments { get; } = new();

    public RangeObservableCollection<CategoryFilterOption> CategoryFilters { get; }

    public RangeObservableCollection<StatusFilterOption> StatusFilters { get; }

    public RangeObservableCollection<HostFilterOption> HostFilters { get; } = new();

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CategoryFilterOption? _selectedCategoryFilter;

    [ObservableProperty]
    private StatusFilterOption? _selectedStatusFilter;

    [ObservableProperty]
    private HostFilterOption? _selectedHostFilter;

    [ObservableProperty]
    private AppointmentListItemDto? _selectedAppointment;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _summaryText = "0 görüşme";

    [ObservableProperty]
    private string _emptyMessage = "Bu tarihte görüşme yok.";

    [ObservableProperty]
    private bool _hasAppointments;

    [ObservableProperty]
    private AppointmentListItemDto? _nextAppointment;

    public bool HasNextAppointment => NextAppointment is not null;

    public string NextAppointmentText
    {
        get
        {
            if (NextAppointment is null)
                return string.Empty;

            var day = NextAppointment.StartsAtLocal.Date == DateTime.Today
                ? string.Empty
                : $"{NextAppointment.DateLabel} ";

            return $"Sıradaki: {day}{NextAppointment.TimeLabel} — {NextAppointment.PersonFullName}";
        }
    }

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

    public string DateLabel => SelectedDate.ToString("dd/MM/yyyy dddd");

    public bool CanCreate => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool CanDelete => _currentUser.IsInRole(UserRole.Admin);

    public bool CanFilterHosts => !_currentUser.IsInRole(UserRole.Host);

    public bool CanShareDailyList => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    public bool IsHostRole => _currentUser.IsInRole(UserRole.Host);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await EnsureFiltersAsync();

            var categoryId = SelectedCategoryFilter?.Id;
            var status = SelectedStatusFilter?.Value;
            var useSearch = IsSearchActive;

            var hostUserId = SelectedHostFilter?.Id;
            var rows = useSearch
                ? await _appointments.SearchAsync(SearchText, categoryId, status, null, null, hostUserId)
                : await _appointments.SearchAsync(null, categoryId, status, SelectedDate, SelectedDate, hostUserId);

            Appointments.ReplaceWith(rows);

            HasAppointments = rows.Count > 0;
            EmptyMessage = useSearch
                ? "Aramanızla eşleşen görüşme bulunamadı."
                : "Bu tarihte görüşme yok. Sağ alttaki + düğmesiyle yeni görüşme ekleyebilirsiniz.";

            var completed = 0;
            var noShow = 0;
            foreach (var row in rows)
            {
                if (row.Status == AppointmentStatus.Completed)
                    completed++;
                else if (row.Status == AppointmentStatus.NoShow)
                    noShow++;
            }

            SummaryText = $"{rows.Count} görüşme · {completed} tamamlandı · {noShow} gelmedi";

            NextAppointment = await _appointments.GetNextAppointmentAsync();

            if (SelectedAppointment is not null
                && Appointments.All(x => x.Id != SelectedAppointment.Id))
            {
                SelectedAppointment = null;
            }
        }
        catch (Exception ex)
        {
            _dialogs.Error($"Görüşme listesi yüklenemedi: {ex.Message}", "Günlük liste");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PreviousDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(-1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextDayAsync()
    {
        SelectedDate = SelectedDate.AddDays(1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoTodayAsync()
    {
        SelectedDate = DateTime.Today;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressReload = true;
        SearchText = string.Empty;
        SelectedCategoryFilter = CategoryFilters[0];
        SelectedStatusFilter = StatusFilters[0];
        SelectedHostFilter = HostFilters.Count > 0 ? HostFilters[0] : null;
        _suppressReload = false;
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task OpenNewAsync()
    {
        if (await _appointmentDialogs.ShowCreateAsync(SelectedDate))
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenDetailAsync()
    {
        if (SelectedAppointment is null)
            return;

        if (await _appointmentDialogs.ShowDetailAsync(SelectedAppointment.Id))
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasNextAppointment))]
    private async Task OpenNextAsync()
    {
        if (NextAppointment is null)
            return;

        if (await _appointmentDialogs.ShowDetailAsync(NextAppointment.Id))
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedAppointment is null)
            return;

        if (!_dialogs.Confirm(
                $"{SelectedAppointment.PersonFullName} · {SelectedAppointment.DateLabel} {SelectedAppointment.TimeLabel}\n\nGörüşme silinsin mi? 30 gün içinde yönetici tarafından geri alınabilir.",
                "Görüşmeyi sil"))
            return;

        var result = await _appointments.DeleteAsync(SelectedAppointment.Id);
        if (!result.Success)
        {
            _dialogs.Error(result.Error ?? "Görüşme silinemedi.", "Görüşme silinemedi");
            return;
        }

        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task OpenDeletedAsync()
    {
        if (await _appointmentDialogs.ShowDeletedAsync())
            await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanShareDailyList))]
    private async Task ShareDailyListAsync()
        => await _appointmentDialogs.ShowShareDailyListAsync(SelectedDate);

    private async Task EnsureFiltersAsync()
    {
        if (_filtersLoaded)
            return;

        var selectedCategoryId = SelectedCategoryFilter?.Id;
        var selectedHostId = SelectedHostFilter?.Id;

        var categoriesTask = _categories.GetAllAsync();
        var hostsTask = _hosts.GetSelectableHostsAsync();
        await Task.WhenAll(categoriesTask, hostsTask);

        var categories = await categoriesTask;
        var categoryOptions = new List<CategoryFilterOption>(categories.Count + 1)
        {
            new(null, "Tüm kategoriler")
        };

        categoryOptions.AddRange(categories.Select(category => new CategoryFilterOption(
            category.Id,
            category.IsActive ? category.Name : $"{category.Name} (pasif)")));

        CategoryFilters.ReplaceWith(categoryOptions);

        var hosts = await hostsTask;
        var hostOptions = new List<HostFilterOption>(hosts.Count + 1)
        {
            new(null, "Tüm görüşmeciler")
        };

        hostOptions.AddRange(hosts.Select(host => new HostFilterOption(host.Id, host.DisplayName)));
        HostFilters.ReplaceWith(hostOptions);

        _suppressReload = true;
        SelectedCategoryFilter = CategoryFilters.FirstOrDefault(x => x.Id == selectedCategoryId)
                                 ?? CategoryFilters[0];
        SelectedHostFilter = HostFilters.FirstOrDefault(x => x.Id == selectedHostId)
                             ?? HostFilters[0];
        _suppressReload = false;

        _filtersLoaded = true;
    }

    private bool HasSelection()
        => SelectedAppointment is not null && !IsBusy;

    private void ApplyDate(DateTime date)
    {
        _suppressReload = true;
        SelectedDate = date.Date;
        _suppressReload = false;
        _ = LoadAsync();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(DateLabel));
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchActive));
        if (_suppressReload)
            return;

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    partial void OnSelectedCategoryFilterChanged(CategoryFilterOption? value)
    {
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSelectedStatusFilterChanged(StatusFilterOption? value)
    {
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSelectedHostFilterChanged(HostFilterOption? value)
    {
        if (!_suppressReload)
            _ = LoadAsync();
    }

    partial void OnSelectedAppointmentChanged(AppointmentListItemDto? value)
    {
        OpenDetailCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    partial void OnNextAppointmentChanged(AppointmentListItemDto? value)
    {
        OnPropertyChanged(nameof(HasNextAppointment));
        OnPropertyChanged(nameof(NextAppointmentText));
        OpenNextCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OpenDetailCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }
}
