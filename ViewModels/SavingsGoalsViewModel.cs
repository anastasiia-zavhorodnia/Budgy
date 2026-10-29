using System.Collections.ObjectModel;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class SavingsGoalsViewModel(
    ISavingsGoalService goalService,
    IAccountService accountService,
    IAccessService access,
    ISettingsService settings,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<SavingsGoal> _goals = new();
    [ObservableProperty] private SavingsGoal? _selectedGoal;

    partial void OnSelectedGoalChanged(SavingsGoal? value)
    {
        if (value is not null) RunSafe(() => OpenMenuAsync(value));
    }

    protected override async Task LoadCoreAsync()
        => Goals = new(await goalService.GetAllAsync());

    [RelayCommand]
    private Task CreateGoal() => nav.GoToAsync("AddGoal");

    private async Task OpenMenuAsync(SavingsGoal goal)
    {
        try
        {
            var title = $"{goal.Name}: {goal.CurrentAmount:N0} / {goal.TargetAmount:N0} ₴ ({goal.Progress:P0})";
            var choice = await nav.ActionSheetAsync(title, "Закрити", "Видалити ціль",
                "Поповнити", "Зняти кошти", "Історія поповнень");

            switch (choice)
            {
                case "Поповнити": await MoveMoneyAsync(goal, deposit: true); break;
                case "Зняти кошти": await MoveMoneyAsync(goal, deposit: false); break;
                case "Історія поповнень": await ShowHistoryAsync(goal); break;
                case "Видалити ціль":
                    var gate = await access.RequireAdultAsync();
                    if (!gate.Success)
                    {
                        await nav.AlertAsync("Немає доступу", gate.Error ?? AccessMessages.Denied);
                        break;
                    }
                    if (await nav.ConfirmAsync("Видалення", $"Видалити ціль «{goal.Name}»?"))
                    {
                        var r = await goalService.DeleteAsync(goal.Id);
                        if (!r.Success) await nav.AlertAsync("Не вдалося видалити", r.Error ?? string.Empty);
                    }
                    break;
            }
        }
        finally
        {
            SelectedGoal = null;
            await LoadAsync();
        }
    }

    private async Task MoveMoneyAsync(SavingsGoal goal, bool deposit)
    {
        var accounts = (await accountService.GetAllAsync(activeOnly: true))
            .Where(a => a.Currency == settings.Currency).ToList();
        if (accounts.Count == 0)
        {
            await nav.AlertAsync("Рахунки", $"Немає активних рахунків у валюті {settings.Currency}.");
            return;
        }

        var account = await nav.PickAsync(deposit ? "З якого рахунку?" : "На який рахунок?", accounts,
            a => $"{a.Name} ({a.Balance:N0})");
        if (account is null) return;

        var text = await nav.PromptAsync(deposit ? "Поповнення цілі" : "Зняття з цілі", "Сума", numeric: true);
        if (text is null) return;
        if (!Input.TryParseAmount(text, out var amount))
        {
            await nav.AlertAsync("Некоректна сума", "Введіть число більше нуля.");
            return;
        }

        var result = deposit
            ? await goalService.ContributeAsync(goal.Id, account.Id, amount)
            : await goalService.WithdrawAsync(goal.Id, account.Id, amount);

        if (!result.Success) await nav.AlertAsync("Помилка", result.Error ?? string.Empty);
    }

    private async Task ShowHistoryAsync(SavingsGoal goal)
    {
        var items = await goalService.GetContributionsAsync(goal.Id);
        if (items.Count == 0)
        {
            await nav.AlertAsync(goal.Name, "Поповнень ще не було.");
            return;
        }

        var lines = new List<string>();
        foreach (var c in items.Take(10))
        {
            var accountName = (await accountService.GetByIdAsync(c.AccountId))?.Name ?? "—";
            var sign = c.Amount >= 0 ? "+" : "−";
            lines.Add($"{c.Date:dd.MM.yyyy}  {sign}{Math.Abs(c.Amount):N0} ₴  ({accountName})");
        }
        await nav.AlertAsync(goal.Name, string.Join("\n", lines));
    }
}
