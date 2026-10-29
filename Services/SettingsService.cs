using Budgy.Models;

namespace Budgy.Services;

public interface ISettingsService
{
    string UserName { get; set; }
    string Email { get; set; }
    Currency Currency { get; set; }
    ThemeMode Theme { get; set; }
    bool NotificationsEnabled { get; set; }
    /// <summary>Автоматично фіксувати координати при додаванні нової операції.</summary>
    bool AutoLocationEnabled { get; set; }
    BudgetMode BudgetMode { get; set; }
    /// <summary>Id активного профілю (0 – профіль не обрано, режим початкового налаштування).</summary>
    int CurrentMemberId { get; set; }

    event EventHandler? BudgetModeChanged;
    void ApplyTheme();
    void Reset();
}

/// <summary>Збереження налаштувань через Preferences (переживають перезапуск додатка).</summary>
public class SettingsService : ISettingsService
{
    public event EventHandler? BudgetModeChanged;

    public string UserName
    {
        get => Preferences.Default.Get(nameof(UserName), string.Empty);
        set => Preferences.Default.Set(nameof(UserName), value);
    }

    public string Email
    {
        get => Preferences.Default.Get(nameof(Email), string.Empty);
        set => Preferences.Default.Set(nameof(Email), value);
    }

    public Currency Currency
    {
        get => (Currency)Preferences.Default.Get(nameof(Currency), (int)Currency.UAH);
        set => Preferences.Default.Set(nameof(Currency), (int)value);
    }

    public ThemeMode Theme
    {
        get => (ThemeMode)Preferences.Default.Get(nameof(Theme), (int)ThemeMode.System);
        set { Preferences.Default.Set(nameof(Theme), (int)value); ApplyTheme(); }
    }

    public bool NotificationsEnabled
    {
        get => Preferences.Default.Get(nameof(NotificationsEnabled), true);
        set => Preferences.Default.Set(nameof(NotificationsEnabled), value);
    }

    public bool AutoLocationEnabled
    {
        get => Preferences.Default.Get(nameof(AutoLocationEnabled), true);
        set => Preferences.Default.Set(nameof(AutoLocationEnabled), value);
    }

    public BudgetMode BudgetMode
    {
        get => (BudgetMode)Preferences.Default.Get(nameof(BudgetMode), (int)BudgetMode.Household);
        set
        {
            if (BudgetMode == value) return;
            Preferences.Default.Set(nameof(BudgetMode), (int)value);
            BudgetModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int CurrentMemberId
    {
        get => Preferences.Default.Get(nameof(CurrentMemberId), 0);
        set => Preferences.Default.Set(nameof(CurrentMemberId), value);
    }

    public void ApplyTheme()
    {
        if (Application.Current is null) return;
        Application.Current.UserAppTheme = Theme switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    public void Reset()
    {
        Preferences.Default.Clear();
        ApplyTheme();
        BudgetModeChanged?.Invoke(this, EventArgs.Empty);
    }
}
