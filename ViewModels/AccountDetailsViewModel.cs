using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

[QueryProperty(nameof(AccountId), "AccountId")]
public partial class AccountDetailsViewModel(
    IAccountService accountService,
    ITransactionService transactionService,
    IAccessService access,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private int _accountId;
    [ObservableProperty] private Account? _account;

    protected override async Task LoadCoreAsync()
    {
        var acc = await accountService.GetByIdAsync(AccountId);
        if (acc is null) return;

        var list = await transactionService.QueryAsync(new TransactionFilter { AccountId = AccountId });
        acc.Transactions = list.ToList();
        Account = acc;
        OnPropertyChanged(nameof(Account)); // оновити прив'язки, навіть якщо посилання те саме
    }

    [RelayCommand]
    private async Task TopUp()
    {
        var text = await nav.PromptAsync("Поповнення", "Сума поповнення", numeric: true);
        if (text is null) return;
        if (!Input.TryParseAmount(text, out var amount))
        {
            await nav.AlertAsync("Некоректна сума", "Введіть число більше нуля.");
            return;
        }
        var result = await accountService.TopUpAsync(AccountId, amount);
        if (!result.Success) await nav.AlertAsync("Помилка", result.Error ?? string.Empty);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Transfer()
    {
        if (Account is null) return;
        var targets = (await accountService.GetAllAsync(activeOnly: true))
            .Where(a => a.Id != Account.Id && a.Currency == Account.Currency).ToList();
        if (targets.Count == 0)
        {
            await nav.AlertAsync("Переказ", "Немає іншого активного рахунку в цій самій валюті.");
            return;
        }

        var target = await nav.PickAsync("Переказ на рахунок", targets, a => $"{a.Name} ({a.Balance:N0})");
        if (target is null) return;

        var text = await nav.PromptAsync("Переказ", $"Сума переказу на «{target.Name}»", numeric: true);
        if (text is null) return;
        if (!Input.TryParseAmount(text, out var amount))
        {
            await nav.AlertAsync("Некоректна сума", "Введіть число більше нуля.");
            return;
        }
        var result = await accountService.TransferAsync(Account.Id, target.Id, amount);
        if (!result.Success) await nav.AlertAsync("Помилка", result.Error ?? string.Empty);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (Account is null) return;
        var toggle = Account.IsActive ? "Деактивувати" : "Активувати";
        var choice = await nav.ActionSheetAsync("Редагування рахунку", "Скасувати", "Видалити", "Змінити назву", "Змінити банк", toggle);

        switch (choice)
        {
            case "Змінити назву":
                var name = await nav.PromptAsync("Назва рахунку", "Нова назва", Account.Name);
                if (name is not null) await Save(a => a.Name = name);
                break;
            case "Змінити банк":
                var bank = await nav.PromptAsync("Банк", "Назва банку", Account.BankName);
                if (bank is not null) await Save(a => a.BankName = bank);
                break;
            case "Деактивувати":
            case "Активувати":
                await Save(a => a.IsActive = !a.IsActive);
                break;
            case "Видалити":
                // рання перевірка прав, щоб не питати підтвердження марно (сервіс перевіряє ще раз)
                var gate = await access.RequireAdultAsync();
                if (!gate.Success)
                {
                    await nav.AlertAsync("Немає доступу", gate.Error ?? AccessMessages.Denied);
                    return;
                }
                var (txCount, contributionCount) = await accountService.GetRelatedCountsAsync(Account.Id);
                var question = txCount + contributionCount == 0
                    ? $"Видалити рахунок «{Account.Name}»?"
                    : $"Видалити рахунок «{Account.Name}»? Разом з ним буде безповоротно видалено операцій: {txCount} та внесків до цілей: {contributionCount}.";
                if (!await nav.ConfirmAsync("Видалення рахунку", question)) return;
                var result = await accountService.DeleteAsync(Account.Id);
                if (!result.Success) await nav.AlertAsync("Не вдалося видалити", result.Error ?? string.Empty);
                else await nav.GoBackAsync();
                break;
        }
    }

    private async Task Save(Action<Account> change)
    {
        if (Account is null) return;
        // редагуємо копію, сервіс сам валідує й переносить зміни
        var copy = new Account
        {
            Id = Account.Id, Name = Account.Name, BankName = Account.BankName,
            AccountType = Account.AccountType, IsActive = Account.IsActive
        };
        change(copy);
        var result = await accountService.UpdateAsync(copy);
        if (!result.Success) await nav.AlertAsync("Помилка", result.Error ?? string.Empty);
        await LoadAsync();
    }
}
