using System.Collections.ObjectModel;
using CgpRandevuTakip.Application.AuditLogs;
using CgpRandevuTakip.Application.Users;
using CgpRandevuTakip.Desktop.Common;
using CgpRandevuTakip.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CgpRandevuTakip.Desktop.ViewModels;

public sealed record AuditUserFilterOption(string? Id, string Label);

public sealed record AuditActionFilterOption(AuditAction? Value, string Label);

public partial class AuditLogViewModel : ObservableObject
{
    private readonly IAuditLogService _audit;
    private readonly IUserService _users;

    private bool _lookupsLoaded;

    public AuditLogViewModel(IAuditLogService audit, IUserService users)
    {
        _audit = audit;
        _users = users;
        PageSizeOptions = new ObservableCollection<int> { 25, 50, 100 };
        _selectedPageSize = 50;
    }

    public RangeObservableCollection<AuditLogEntryDto> Entries { get; } = new();

    public RangeObservableCollection<AuditUserFilterOption> UserFilters { get; } = new();

    public RangeObservableCollection<AuditActionFilterOption> ActionFilters { get; } = new();

    public ObservableCollection<int> PageSizeOptions { get; }

    [ObservableProperty]
    private AuditUserFilterOption? _selectedUserFilter;

    [ObservableProperty]
    private AuditActionFilterOption? _selectedActionFilter;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private int _selectedPageSize = 50;

    [ObservableProperty]
    private int _page = 1;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _summaryText = "Kayıt bulunamadı.";

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasEntries => Entries.Count > 0;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage { get; private set; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await EnsureLookupsAsync();
        await QueryAsync(1);
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync()
        => await QueryAsync(1);

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        SelectedUserFilter = UserFilters.Count > 0 ? UserFilters[0] : null;
        SelectedActionFilter = ActionFilters.Count > 0 ? ActionFilters[0] : null;
        FromDate = null;
        ToDate = null;
        await QueryAsync(1);
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private async Task PreviousPageAsync()
        => await QueryAsync(Page - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private async Task NextPageAsync()
        => await QueryAsync(Page + 1);

    private async Task EnsureLookupsAsync()
    {
        if (_lookupsLoaded)
            return;

        var users = await _users.GetAllAsync();

        UserFilters.ReplaceWith(new[]
        {
            new AuditUserFilterOption(null, "Tüm kullanıcılar")
        }.Concat(users.Select(x => new AuditUserFilterOption(x.Id, x.DisplayName))));

        var actions = Enum.GetValues<AuditAction>()
            .Select(x => new AuditActionFilterOption(x, AuditActionText.Get(x)))
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ActionFilters.ReplaceWith(new[]
        {
            new AuditActionFilterOption(null, "Tüm işlemler")
        }.Concat(actions));

        SelectedUserFilter = UserFilters[0];
        SelectedActionFilter = ActionFilters[0];
        _lookupsLoaded = true;
    }

    private async Task QueryAsync(int page)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var filter = new AuditLogFilter(
                SelectedUserFilter?.Id,
                SelectedActionFilter?.Value,
                FromDate,
                ToDate);

            var result = await _audit.QueryAsync(filter, page, SelectedPageSize);

            if (result.Error is not null)
            {
                ErrorMessage = result.Error;
                Entries.ReplaceWith(Array.Empty<AuditLogEntryDto>());
                SummaryText = "Kayıt bulunamadı.";
                Page = 1;
                HasNextPage = false;
            }
            else
            {
                Entries.ReplaceWith(result.Items);
                Page = result.Page;
                HasNextPage = result.HasNext;
                SummaryText = result.SummaryText;
            }

            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Denetim kayıtları yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedPageSizeChanged(int value)
    {
        if (_lookupsLoaded)
            _ = QueryAsync(1);
    }
}
