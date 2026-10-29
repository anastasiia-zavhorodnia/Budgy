using System.Collections.ObjectModel;
using Budgy.Converters;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class HouseholdViewModel(
    IHouseholdService householdService,
    IAccessService access,
    ISettingsService settings,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private Household? _household;
    [ObservableProperty] private ObservableCollection<HouseholdMember> _members = new();
    [ObservableProperty] private HouseholdMember? _selectedMember;

    public BudgetMode BudgetMode => settings.BudgetMode;

    partial void OnSelectedMemberChanged(HouseholdMember? value)
    {
        if (value is not null) RunSafe(() => OpenMenuAsync(value));
    }

    protected override async Task LoadCoreAsync()
    {
        Household = await householdService.GetHouseholdAsync();
        OnPropertyChanged(nameof(Household)); // оновити назву після перейменування
        Members = new(await householdService.GetMembersAsync());
        OnPropertyChanged(nameof(BudgetMode));
    }

    /// <summary>Перемикання бюджету: "Personal" або "Household".</summary>
    [RelayCommand]
    private Task SetMode(string? mode)
    {
        settings.BudgetMode = mode == nameof(Models.BudgetMode.Personal) ? Models.BudgetMode.Personal : Models.BudgetMode.Household;
        return LoadAsync();
    }

    [RelayCommand]
    private async Task Rename()
    {
        if (!await EnsureAdultAsync()) return;
        var name = await nav.PromptAsync("Домогосподарство", "Нова назва", Household?.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        await Report(await householdService.RenameAsync(name));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddMember()
    {
        if (!await EnsureAdultAsync()) return;

        var name = await nav.PromptAsync("Новий член сім'ї", "Ім'я");
        if (string.IsNullOrWhiteSpace(name)) return;

        var role = await PickRoleAsync();
        if (role is null) return;

        await Report(await householdService.AddMemberAsync(new HouseholdMember { Name = name, Role = role.Value }));
        await LoadAsync();
    }

    private async Task OpenMenuAsync(HouseholdMember member)
    {
        try
        {
            var choice = await nav.ActionSheetAsync($"{member.Name} ({EnumText.Of(member.Role)})", "Скасувати", "Видалити",
                "Це я (перемкнути профіль)", "Змінити ім'я", "Змінити роль");

            switch (choice)
            {
                case "Це я (перемкнути профіль)":
                    var switched = await householdService.SetCurrentMemberAsync(member.Id);
                    if (!switched.Success) { await Report(switched); break; }
                    await nav.AlertAsync("Профіль змінено",
                        member.Role == FamilyRole.Child
                            ? $"Тепер ви — {member.Name} (Дитина). Видаляти рахунки, категорії, цілі й членів сім'ї та змінювати налаштування домогосподарства неможливо."
                            : $"Тепер ви — {member.Name} ({EnumText.Of(member.Role)}). Доступні всі дії.");
                    break;

                case "Змінити ім'я":
                    if (!await EnsureAdultAsync()) break;
                    var name = await nav.PromptAsync("Ім'я", "Нове ім'я", member.Name);
                    if (!string.IsNullOrWhiteSpace(name))
                        await Report(await householdService.UpdateMemberAsync(new HouseholdMember { Id = member.Id, Name = name, Role = member.Role }));
                    break;

                case "Змінити роль":
                    if (!await EnsureAdultAsync()) break;
                    var role = await PickRoleAsync();
                    if (role is not null)
                        await Report(await householdService.UpdateMemberAsync(new HouseholdMember { Id = member.Id, Name = member.Name, Role = role.Value }));
                    break;

                case "Видалити":
                    if (!await EnsureAdultAsync()) break;
                    if (await nav.ConfirmAsync("Видалення", $"Видалити «{member.Name}» з домогосподарства?"))
                        await Report(await householdService.RemoveMemberAsync(member.Id));
                    break;
            }
        }
        finally
        {
            SelectedMember = null;
            await LoadAsync();
        }
    }

    private Task<FamilyRole?> PickRoleAsync()
        => nav.PickValueAsync("Роль у сім'ї", Enum.GetValues<FamilyRole>(), r => EnumText.Of(r));

    /// <summary>Рання перевірка прав (сервіс перевіряє ще раз).</summary>
    private async Task<bool> EnsureAdultAsync()
    {
        var gate = await access.RequireAdultAsync();
        if (gate.Success) return true;
        await nav.AlertAsync("Немає доступу", gate.Error ?? AccessMessages.Denied);
        return false;
    }

    private async Task Report(OperationResult result)
    {
        if (!result.Success) await nav.AlertAsync("Помилка", result.Error ?? string.Empty);
    }
}
