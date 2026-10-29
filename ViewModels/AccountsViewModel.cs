using System.Collections.ObjectModel;
using System.Globalization;
using Budgy.Converters;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class AccountsViewModel(
    IAccountService accountService,
    ISettingsService settings,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();
    [ObservableProperty] private decimal _totalBalance;
    [ObservableProperty] private Account? _selectedAccount;

    protected override async Task LoadCoreAsync()
    {
        Accounts = new(await accountService.GetAllAsync());
        TotalBalance = await accountService.GetTotalBalanceAsync(settings.Currency);
    }

    [RelayCommand]
    private async Task OpenAccount()
    {
        if (SelectedAccount is null) return;
        var id = SelectedAccount.Id;
        SelectedAccount = null; // щоб повторний тап по тому самому рахунку знову спрацьовував
        await nav.GoToAsync("AccountDetails", new Dictionary<string, object> { ["AccountId"] = id });
    }

    [RelayCommand]
    private async Task AddAccount()
    {
        var name = await nav.PromptAsync("Новий рахунок", "Назва рахунку");
        if (string.IsNullOrWhiteSpace(name)) return;

        var bank = await nav.PromptAsync("Новий рахунок", "Назва банку (необов'язково)") ?? string.Empty;

        var type = await nav.PickValueAsync("Тип рахунку", Enum.GetValues<AccountType>(), t => EnumText.Of(t));
        if (type is null) return;

        var currency = await nav.PickValueAsync("Валюта", Enum.GetValues<Currency>(), c => c.ToString());
        if (currency is null) return;

        var balanceText = await nav.PromptAsync("Початковий баланс", "Сума (можна залишити 0)", "0", numeric: true);
        decimal.TryParse(balanceText?.Trim().Replace(" ", "").Replace(',', '.'),
            NumberStyles.Number, CultureInfo.InvariantCulture, out var balance);

        var result = await accountService.AddAsync(new Account
        {
            Name = name, BankName = bank, AccountType = type.Value, Currency = currency.Value, Balance = balance
        });
        if (!result.Success) await nav.AlertAsync("Не вдалося додати рахунок", result.Error ?? string.Empty);
        await LoadAsync();
    }
}
