using System.Collections.ObjectModel;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class TransactionsViewModel(
    ITransactionService transactionService,
    IAccountService accountService,
    ICategoryService categoryService,
    IAccessService access,
    INavigationService nav) : ViewModelBase
{
    private string _typeFilter = "All";
    private int _version; // відкидає застарілі результати, якщо фільтр змінили швидше, ніж завершився запит

    /// <summary>Ключ активного сегмента (All / Income / Expense) для підсвітки кнопки.</summary>
    public string TypeKey => _typeFilter;

    /// <summary>Показувати операції починаючи з цієї дати.</summary>
    [ObservableProperty] private DateTime _filterDate = DateTime.Today.AddMonths(-2);
    [ObservableProperty] private Category? _filterCategory;
    [ObservableProperty] private Account? _filterAccount;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();
    [ObservableProperty] private ObservableCollection<TransactionGroup> _groupedTransactions = new();

    /// <summary>Натискання на операцію відкриває меню «Редагувати / Видалити».</summary>
    [ObservableProperty] private Transaction? _selectedTransaction;

    partial void OnFilterDateChanged(DateTime value) => RunSafe(ApplyFilterAsync);
    partial void OnFilterCategoryChanged(Category? value) => RunSafe(ApplyFilterAsync);
    partial void OnFilterAccountChanged(Account? value) => RunSafe(ApplyFilterAsync);
    partial void OnSearchTextChanged(string value) => RunSafe(ApplyFilterAsync);

    partial void OnSelectedTransactionChanged(Transaction? value)
    {
        if (value is not null) RunSafe(() => OpenMenuAsync(value));
    }

    protected override async Task LoadCoreAsync()
    {
        Categories = new(await categoryService.GetAllAsync());
        Accounts = new(await accountService.GetAllAsync());
        await ApplyFilterAsync();
    }

    [RelayCommand]
    private Task Filter(string? type)
    {
        _typeFilter = type ?? "All";
        OnPropertyChanged(nameof(TypeKey));
        return ApplyFilterAsync();
    }

    [RelayCommand]
    private Task ClearFilters()
    {
        _typeFilter = "All";
        OnPropertyChanged(nameof(TypeKey));
        FilterDate = DateTime.Today.AddMonths(-2);
        FilterCategory = null;
        FilterAccount = null;
        SearchText = string.Empty;
        return ApplyFilterAsync();
    }

    private async Task ApplyFilterAsync()
    {
        var version = ++_version;

        var filter = new TransactionFilter
        {
            Type = _typeFilter switch
            {
                "Income" => TransactionType.Income,
                "Expense" => TransactionType.Expense,
                _ => null
            },
            From = FilterDate,
            CategoryId = FilterCategory?.Id,
            AccountId = FilterAccount?.Id,
            MemberId = await access.GetActiveMemberFilterAsync(),
            Search = SearchText
        };

        var items = await transactionService.QueryAsync(filter);
        if (version != _version) return; // з'явився новіший запит

        var groups = items
            .GroupBy(t => t.Date.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new TransactionGroup(GroupName(g.Key), g));

        GroupedTransactions = new(groups);
    }

    private static string GroupName(DateTime date)
        => date == DateTime.Today ? "Сьогодні"
         : date == DateTime.Today.AddDays(-1) ? "Вчора"
         : date.ToString("dd.MM.yyyy");

    [RelayCommand]
    private Task AddTransaction() => nav.GoToAsync("AddTransaction");

    [RelayCommand]
    private Task Edit(Transaction? transaction)
        => transaction is null ? Task.CompletedTask : EditAsync(transaction);

    [RelayCommand]
    private Task Delete(Transaction? transaction)
        => transaction is null ? Task.CompletedTask : DeleteTransactionAsync(transaction);

    private async Task OpenMenuAsync(Transaction transaction)
    {
        try
        {
            var sign = transaction.Type == TransactionType.Income ? "+" : "−";
            var title = $"{transaction.Category?.Name} {sign}{transaction.Amount:N0} ₴ · {transaction.Date:dd.MM.yyyy}";

            var choice = await nav.ActionSheetAsync(title, "Закрити", "Видалити", "Редагувати");
            switch (choice)
            {
                case "Редагувати":
                    await EditAsync(transaction);
                    break;
                case "Видалити":
                    await DeleteTransactionAsync(transaction);
                    break;
            }
        }
        finally
        {
            SelectedTransaction = null; // щоб повторне натискання на ту саму операцію знову спрацьовувало
        }
    }

    private Task EditAsync(Transaction transaction)
        => nav.GoToAsync("AddTransaction", new Dictionary<string, object> { ["TransactionId"] = transaction.Id });

    private async Task DeleteTransactionAsync(Transaction transaction)
    {
        // Баланс рахунку перераховується сервісом: витрата повертає гроші, дохід — списує
        var effect = transaction.Type == TransactionType.Expense
            ? $"Баланс рахунку «{transaction.Account?.Name}» збільшиться на {transaction.Amount:N0} ₴."
            : $"Баланс рахунку «{transaction.Account?.Name}» зменшиться на {transaction.Amount:N0} ₴.";

        if (!await nav.ConfirmAsync("Видалення операції", $"Видалити цю операцію? {effect}")) return;

        var result = await transactionService.DeleteAsync(transaction.Id);
        if (!result.Success) await nav.AlertAsync("Не вдалося видалити", result.Error ?? string.Empty);
        await ApplyFilterAsync();
    }
}
