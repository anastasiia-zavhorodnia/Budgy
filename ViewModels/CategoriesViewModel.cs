using System.Collections.ObjectModel;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class CategoriesViewModel(
    ICategoryService categoryService,
    IAccessService access,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private TransactionType _type = TransactionType.Expense;
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedCategory;

    /// <summary>Ключ активного сегмента (Expense / Income).</summary>
    public string TypeKey => Type.ToString();

    partial void OnTypeChanged(TransactionType value) => OnPropertyChanged(nameof(TypeKey));

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value is not null) RunSafe(() => OpenMenuAsync(value));
    }

    protected override async Task LoadCoreAsync()
        => Categories = new(await categoryService.GetAllAsync(Type));

    [RelayCommand]
    private Task SetType(string? type)
    {
        Type = type == "Income" ? TransactionType.Income : TransactionType.Expense;
        return LoadAsync();
    }

    [RelayCommand]
    private async Task AddCategory()
    {
        var name = await nav.PromptAsync("Нова категорія", "Назва");
        if (string.IsNullOrWhiteSpace(name)) return;
        var icon = await nav.PromptAsync("Іконка", "Емодзі для категорії (необов'язково)", "📁");

        var result = await categoryService.AddAsync(new Category { Name = name, Type = Type, Icon = icon });
        if (!result.Success) await nav.AlertAsync("Помилка", result.Error ?? string.Empty);
        await LoadAsync();
    }

    private async Task OpenMenuAsync(Category category)
    {
        try
        {
            var subs = category.Subcategories.Count == 0
                ? "немає"
                : string.Join(", ", category.Subcategories.Select(s => s.Name));

            var choice = await nav.ActionSheetAsync($"{category.Name} — підкатегорії: {subs}", "Закрити", "Видалити категорію",
                "Додати підкатегорію", "Видалити підкатегорію", "Перейменувати");

            switch (choice)
            {
                case "Додати підкатегорію":
                    var name = await nav.PromptAsync("Нова підкатегорія", "Назва");
                    if (!string.IsNullOrWhiteSpace(name))
                        await Report(await categoryService.AddSubcategoryAsync(category.Id, name));
                    break;

                case "Видалити підкатегорію":
                    if (!await EnsureAdultAsync()) break;
                    var sub = await nav.PickAsync("Яку видалити?", category.Subcategories.ToList(), s => s.Name);
                    if (sub is not null) await Report(await categoryService.DeleteSubcategoryAsync(sub.Id));
                    break;

                case "Перейменувати":
                    var newName = await nav.PromptAsync("Перейменування", "Нова назва", category.Name);
                    if (!string.IsNullOrWhiteSpace(newName))
                        await Report(await categoryService.UpdateAsync(
                            new Category { Id = category.Id, Name = newName, Icon = category.Icon }));
                    break;

                case "Видалити категорію":
                    if (!await EnsureAdultAsync()) break;
                    if (await nav.ConfirmAsync("Видалення", $"Видалити категорію «{category.Name}»?"))
                        await Report(await categoryService.DeleteAsync(category.Id));
                    break;
            }
        }
        finally
        {
            SelectedCategory = null;
            await LoadAsync();
        }
    }

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
