using System.Collections.ObjectModel;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class DashboardViewModel(
    IAccountService accountService,
    ITransactionService transactionService,
    IAnalyticsService analytics,
    ISavingsGoalService goalService,
    IAccessService access,
    ISettingsService settings) : ViewModelBase
{
    [ObservableProperty] private string _userName = "друже";
    [ObservableProperty] private decimal _totalBalance;
    [ObservableProperty] private decimal _totalIncome;
    [ObservableProperty] private decimal _totalExpense;
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();
    [ObservableProperty] private ObservableCollection<Transaction> _recentTransactions = new();
    [ObservableProperty] private ObservableCollection<SavingsGoal> _topGoals = new();

    /// <summary>Ключ активного сегмента (Personal / Household).</summary>
    public string ModeKey => settings.BudgetMode.ToString();

    protected override async Task LoadCoreAsync()
    {
        OnPropertyChanged(nameof(ModeKey));
        var member = await access.GetCurrentMemberAsync();
        UserName = member?.Name ?? "друже";

        Accounts = new(await accountService.GetAllAsync(activeOnly: true));
        TotalBalance = await accountService.GetTotalBalanceAsync(settings.Currency);

        // Доходи/витрати за поточний місяць (залежать від режиму бюджету)
        var memberId = await access.GetActiveMemberFilterAsync();
        var today = DateTime.Today;
        var (income, expense) = await analytics.GetTotalsAsync(new DateTime(today.Year, today.Month, 1), today, memberId);
        TotalIncome = income;
        TotalExpense = expense;

        var recent = await transactionService.QueryAsync(new TransactionFilter { MemberId = memberId });
        RecentTransactions = new(recent.Take(5));

        var goals = await goalService.GetAllAsync();
        TopGoals = new(goals.Where(g => !g.IsCompleted).Take(3));
    }

    [RelayCommand]
    private Task SetMode(string? key)
    {
        settings.BudgetMode = key == nameof(BudgetMode.Personal) ? BudgetMode.Personal : BudgetMode.Household;
        return LoadAsync();
    }
}
