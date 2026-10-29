using System.Collections.ObjectModel;
using System.Windows.Input;
using Budgy.Converters;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

/// <summary>Рядок налаштувань у списку (Title, Value, Command).</summary>
public partial class SettingsItem : ObservableObject
{
    public string Title { get; init; } = string.Empty;
    public ICommand? Command { get; init; }
    [ObservableProperty] private string _value = string.Empty;
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IAccessService _access;
    private readonly INavigationService _nav;
    private readonly SettingsItem _profileItem, _currencyItem, _themeItem, _notificationsItem, _locationItem;

    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    public ObservableCollection<SettingsItem> Items { get; } = new();

    public SettingsViewModel(ISettingsService settings, IAccessService access, INavigationService nav)
    {
        _settings = settings;
        _access = access;
        _nav = nav;

        _profileItem = new SettingsItem { Title = "Активний профіль", Command = new AsyncRelayCommand(ShowProfileAsync) };
        _currencyItem = new SettingsItem { Title = "Валюта", Command = new AsyncRelayCommand(ChangeCurrencyAsync) };
        _themeItem = new SettingsItem { Title = "Тема", Command = new AsyncRelayCommand(ChangeThemeAsync) };
        _notificationsItem = new SettingsItem { Title = "Сповіщення", Command = new AsyncRelayCommand(ToggleNotificationsAsync) };

        _locationItem = new SettingsItem { Title = "Геолокація покупки", Command = new AsyncRelayCommand(ToggleLocationAsync) };

        Items.Add(_profileItem);
        Items.Add(_currencyItem);
        Items.Add(_themeItem);
        Items.Add(_notificationsItem);
        Items.Add(_locationItem);
        Items.Add(new SettingsItem { Title = "Про додаток", Command = new AsyncRelayCommand(ShowAboutAsync) });
    }

    protected override Task LoadCoreAsync() => RefreshValuesAsync();

    private async Task RefreshValuesAsync()
    {
        var member = await _access.GetCurrentMemberAsync();
        UserName = member?.Name ?? _settings.UserName;
        Email = _settings.Email;

        _profileItem.Value = member is null ? "не обрано" : $"{member.Name} ({EnumText.Of(member.Role)})";
        _currencyItem.Value = _settings.Currency.ToString();
        _themeItem.Value = EnumText.Of(_settings.Theme);
        _notificationsItem.Value = _settings.NotificationsEnabled ? "Увімкнено" : "Вимкнено";
        _locationItem.Value = _settings.AutoLocationEnabled ? "Автоматично" : "Вимкнено";
    }

    private Task ShowProfileAsync()
        => _nav.AlertAsync("Профіль", "Щоб змінити активний профіль, відкрийте екран «Сім'я» і натисніть на члена сім'ї.");

    private async Task ChangeCurrencyAsync()
    {
        // основна валюта — загальне налаштування домогосподарства: Дитині заборонено
        var gate = await _access.RequireAdultAsync();
        if (!gate.Success)
        {
            await _nav.AlertAsync("Немає доступу", gate.Error ?? AccessMessages.Denied);
            return;
        }

        var currency = await _nav.PickValueAsync("Основна валюта", Enum.GetValues<Currency>(), c => c.ToString());
        if (currency is null) return;
        _settings.Currency = currency.Value;
        await RefreshValuesAsync();
    }

    private async Task ChangeThemeAsync()
    {
        var theme = await _nav.PickValueAsync("Тема оформлення", Enum.GetValues<ThemeMode>(), t => EnumText.Of(t));
        if (theme is null) return;
        _settings.Theme = theme.Value; // особисте налаштування, застосовується одразу
        await RefreshValuesAsync();
    }

    private async Task ToggleNotificationsAsync()
    {
        _settings.NotificationsEnabled = !_settings.NotificationsEnabled;
        await RefreshValuesAsync();
    }

    private async Task ToggleLocationAsync()
    {
        _settings.AutoLocationEnabled = !_settings.AutoLocationEnabled;
        await RefreshValuesAsync();
    }

    private Task ShowAboutAsync()
        => _nav.AlertAsync("Budgy", $"Версія {AppInfo.Current.VersionString}\nОблік фінансів домогосподарства.");

    /// <summary>"Вийти" = скинути налаштування користувача до початкових (лише для дорослих).</summary>
    [RelayCommand]
    private async Task Logout()
    {
        // скидання знімає активний профіль, тому Дитині це заборонено (інакше обхід обмежень)
        var gate = await _access.RequireAdultAsync();
        if (!gate.Success)
        {
            await _nav.AlertAsync("Немає доступу", gate.Error ?? AccessMessages.Denied);
            return;
        }

        if (!await _nav.ConfirmAsync("Вихід", "Скинути налаштування профілю до початкових?")) return;
        _settings.Reset();
        await RefreshValuesAsync();
    }
}
