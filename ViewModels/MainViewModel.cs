using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Budgy.Models;
using Budgy.Services;

namespace Budgy.ViewModels;

/// <summary>Загальний стан додатка: режим бюджету (особисті / домогосподарство) та навігація.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly INavigationService _nav;

    public MainViewModel(ISettingsService settings, INavigationService nav)
    {
        _settings = settings;
        _nav = nav;
        _settings.BudgetModeChanged += (_, _) => RaiseModeChanged();
    }

    public BudgetMode BudgetMode => _settings.BudgetMode;
    public bool IsHouseholdMode => BudgetMode == BudgetMode.Household;
    public string ModeTitle => IsHouseholdMode ? "Домогосподарство" : "Особисті";

    [RelayCommand]
    private void SetBudgetMode(string? mode)
        => _settings.BudgetMode = mode == nameof(BudgetMode.Personal) ? BudgetMode.Personal : BudgetMode.Household;

    [RelayCommand]
    private Task GoTo(string? route) => string.IsNullOrEmpty(route) ? Task.CompletedTask : _nav.GoToAsync(route);

    private void RaiseModeChanged()
    {
        OnPropertyChanged(nameof(BudgetMode));
        OnPropertyChanged(nameof(IsHouseholdMode));
        OnPropertyChanged(nameof(ModeTitle));
    }
}
