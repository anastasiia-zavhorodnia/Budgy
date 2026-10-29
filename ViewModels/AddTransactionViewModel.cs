using System.Collections.ObjectModel;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

/// <summary>Додавання транзакції; якщо передано TransactionId – редагування існуючої.</summary>
[QueryProperty(nameof(TransactionId), "TransactionId")]
public partial class AddTransactionViewModel(
    ITransactionService transactionService,
    IAccountService accountService,
    ICategoryService categoryService,
    IHouseholdService householdService,
    IAccessService access,
    IReceiptService receipts,
    IMerchantService merchants,
    ISettingsService settings,
    INavigationService nav) : ViewModelBase
{
    private bool _loaded;
    private string? _originalImagePath; // фото, яке вже збережене в БД (під час редагування)

    [ObservableProperty] private int _transactionId;
    [ObservableProperty] private TransactionType _type = TransactionType.Expense;
    [ObservableProperty] private decimal? _amount;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string? _description;

    // Місце покупки (магазин / мережа): вручну або швидкий вибір зі довідника
    [ObservableProperty] private string? _merchant;
    [ObservableProperty] private ObservableCollection<MerchantInfo> _merchantSuggestions = new();
    [ObservableProperty] private MerchantInfo? _selectedSuggestion;

    // Чек і місце покупки
    [ObservableProperty] private string? _imagePath;
    [ObservableProperty] private double? _latitude;
    [ObservableProperty] private double? _longitude;
    [ObservableProperty] private bool _isLocating;

    [ObservableProperty] private ObservableCollection<Account> _accounts = new();
    [ObservableProperty] private Account? _selectedAccount;
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private ObservableCollection<Subcategory> _subcategories = new();
    [ObservableProperty] private Subcategory? _selectedSubcategory;
    [ObservableProperty] private ObservableCollection<HouseholdMember> _members = new();
    [ObservableProperty] private HouseholdMember? _selectedMember;

    public bool IsEditing => TransactionId > 0;

    /// <summary>Ключ активного сегмента (Expense / Income).</summary>
    public string TypeKey => Type.ToString();

    public bool HasPhoto => !string.IsNullOrEmpty(ImagePath);
    public ImageSource? ReceiptPreview
        => HasPhoto && File.Exists(ImagePath!) ? ImageSource.FromFile(ImagePath!) : null;

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
    public string LocationText
        => IsLocating ? "Визначаємо координати…"
         : HasLocation ? $"📍 {Latitude:F5}, {Longitude:F5}"
         : "Координати не визначено";

    partial void OnTypeChanged(TransactionType value) => OnPropertyChanged(nameof(TypeKey));

    partial void OnMerchantChanged(string? value)
        => MerchantSuggestions = new(merchants.Search(value));

    partial void OnSelectedSuggestionChanged(MerchantInfo? value)
    {
        if (value is null) return;
        ApplyMerchant(value);
        SelectedSuggestion = null; // щоб повторний вибір того самого місця знову спрацьовував
    }

    partial void OnImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPhoto));
        OnPropertyChanged(nameof(ReceiptPreview));
    }

    partial void OnLatitudeChanged(double? value) => RaiseLocationChanged();
    partial void OnLongitudeChanged(double? value) => RaiseLocationChanged();
    partial void OnIsLocatingChanged(bool value) => OnPropertyChanged(nameof(LocationText));

    private void RaiseLocationChanged()
    {
        OnPropertyChanged(nameof(HasLocation));
        OnPropertyChanged(nameof(LocationText));
    }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        Subcategories = new(value?.Subcategories ?? new List<Subcategory>());
        SelectedSubcategory = null;
    }

    protected override async Task LoadCoreAsync()
    {
        if (_loaded) return;
        _loaded = true;

        MerchantSuggestions = new(merchants.Search(null)); // популярні місця

        Accounts = new(await accountService.GetAllAsync(activeOnly: true));

        // Дитина бачить у списку лише себе: записати витрату від чужого імені вона не може
        var current = await access.GetCurrentMemberAsync();
        var allMembers = await householdService.GetMembersAsync();
        Members = new(current?.Role == FamilyRole.Child ? allMembers.Where(m => m.Id == current.Id) : allMembers);

        if (!IsEditing)
        {
            // База порожня при першому запуску — підказуємо, що потрібно створити
            var missing = new List<string>();
            if (Accounts.Count == 0) missing.Add("рахунок");
            if ((await categoryService.GetAllAsync()).Count == 0) missing.Add("категорію");
            if (Members.Count == 0) missing.Add("члена сім'ї (екран «Сім'я»)");
            if (missing.Count > 0)
            {
                await nav.AlertAsync("Спочатку налаштуйте дані",
                    $"Щоб додати операцію, створіть: {string.Join(", ", missing)}.");
                await nav.GoBackAsync();
                return;
            }
        }

        if (IsEditing && await transactionService.GetByIdAsync(TransactionId) is { } t)
        {
            Type = t.Type;
            await LoadCategoriesAsync();

            if (Accounts.All(a => a.Id != t.AccountId) && await accountService.GetByIdAsync(t.AccountId) is { } old)
                Accounts.Add(old); // операція на неактивному рахунку

            Amount = t.Amount;
            Date = t.Date;
            Description = t.Description;
            Merchant = t.Merchant;
            _originalImagePath = t.ImagePath;
            ImagePath = t.ImagePath;
            Latitude = t.Latitude;
            Longitude = t.Longitude;
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == t.AccountId);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == t.CategoryId);
            SelectedSubcategory = Subcategories.FirstOrDefault(s => s.Id == t.SubcategoryId);
            SelectedMember = Members.FirstOrDefault(m => m.Id == t.HouseholdMemberId);
        }
        else
        {
            await LoadCategoriesAsync();
            SelectedAccount = Accounts.FirstOrDefault();
            SelectedMember = Members.FirstOrDefault(m => m.Id == current?.Id) ?? Members.FirstOrDefault();

            // Автоматично фіксуємо місце покупки (не блокуючи форму; відмова лише логується)
            if (settings.AutoLocationEnabled)
                RunSafe(() => FetchLocationAsync(silent: true));
        }
    }

    private async Task LoadCategoriesAsync()
    {
        Categories = new(await categoryService.GetAllAsync(Type));
        SelectedCategory = null;
    }

    [RelayCommand]
    private Task SetType(string? type)
    {
        Type = type == "Income" ? TransactionType.Income : TransactionType.Expense;
        return LoadCategoriesAsync(); // категорії залежать від типу
    }

    // ---------------- Місце покупки (довідник) ----------------

    /// <summary>Двоетапний вибір: група (Супермаркети, АЗС, ...) → конкретна мережа.</summary>
    [RelayCommand]
    private async Task BrowseMerchants()
    {
        var group = await nav.PickAsync("Тип місця покупки", merchants.Groups, g => g);
        if (group is null) return;

        var place = await nav.PickAsync(group, merchants.GetByGroup(group), m => m.Display);
        if (place is not null) ApplyMerchant(place);
    }

    private void ApplyMerchant(MerchantInfo place)
    {
        Merchant = place.Display;

        // Якщо категорію ще не обрано — підказуємо її за типом місця (Сільпо → Продукти, OKKO → Транспорт)
        if (SelectedCategory is null && Type == TransactionType.Expense && place.CategoryHint is { } hint)
        {
            var match = Categories.FirstOrDefault(c => c.Name.Equals(hint, StringComparison.OrdinalIgnoreCase));
            if (match is not null) SelectedCategory = match;
        }
    }

    // ---------------- Фото чека ----------------

    [RelayCommand]
    private async Task TakePhoto() => await AttachPhotoAsync(await receipts.CapturePhotoAsync());

    [RelayCommand]
    private async Task PickPhoto() => await AttachPhotoAsync(await receipts.PickPhotoAsync());

    [RelayCommand]
    private void RemovePhoto()
    {
        DiscardSessionPhoto();
        ImagePath = null;
    }

    private async Task AttachPhotoAsync(PhotoResult result)
    {
        if (result.Cancelled) return;
        if (!result.Success)
        {
            await nav.AlertAsync("Фото чека", result.Error ?? "Не вдалося додати фото.");
            return;
        }
        DiscardSessionPhoto();
        ImagePath = result.FilePath;
    }

    /// <summary>Видаляє фото, зроблене в цій сесії (але ще не збережене разом з операцією).</summary>
    private void DiscardSessionPhoto()
    {
        if (!string.IsNullOrEmpty(ImagePath) && ImagePath != _originalImagePath)
            receipts.DeletePhoto(ImagePath);
    }

    // ---------------- Геолокація ----------------

    [RelayCommand]
    private Task CaptureLocation() => FetchLocationAsync(silent: false);

    [RelayCommand]
    private void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
    }

    private async Task FetchLocationAsync(bool silent)
    {
        if (IsLocating) return;
        try
        {
            IsLocating = true;
            var result = await receipts.GetCurrentLocationAsync();
            if (result.Success)
            {
                Latitude = result.Latitude;
                Longitude = result.Longitude;
            }
            else if (!silent)
            {
                await nav.AlertAsync("Геолокація", result.Error ?? "Не вдалося визначити координати.");
            }
        }
        finally
        {
            IsLocating = false;
        }
    }

    // ---------------- Збереження ----------------

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy) return;

        var transaction = new Transaction
        {
            Id = TransactionId,
            Amount = Amount ?? 0,
            Date = Date,
            Type = Type,
            AccountId = SelectedAccount?.Id ?? 0,
            CategoryId = SelectedCategory?.Id ?? 0,
            SubcategoryId = SelectedSubcategory?.Id,
            HouseholdMemberId = SelectedMember?.Id ?? 0,
            Description = Description,
            Merchant = Merchant,
            ImagePath = ImagePath,
            Latitude = Latitude,
            Longitude = Longitude
        };

        try
        {
            IsBusy = true;
            var result = IsEditing
                ? await transactionService.UpdateAsync(transaction)
                : await transactionService.AddAsync(transaction);

            if (!result.Success)
            {
                await nav.AlertAsync("Не вдалося зберегти", result.Error ?? string.Empty);
                return;
            }

            // старе фото більше не потрібне, якщо його замінили або прибрали
            if (!string.IsNullOrEmpty(_originalImagePath) && _originalImagePath != ImagePath)
                receipts.DeletePhoto(_originalImagePath);
        }
        finally { IsBusy = false; }

        await nav.GoBackAsync();
    }
}
