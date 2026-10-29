using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Budgy.Models;
using Budgy.Services;

namespace Budgy.ViewModels;

public partial class AddSavingsGoalViewModel(ISavingsGoalService goalService, INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal? _targetAmount;
    [ObservableProperty] private DateTime _deadline = DateTime.Today.AddMonths(6);
    [ObservableProperty] private string? _description;

    [RelayCommand]
    private async Task Create()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var result = await goalService.AddAsync(new SavingsGoal
            {
                Name = Name,
                TargetAmount = TargetAmount ?? 0,
                Deadline = Deadline,
                Description = Description
            });

            if (!result.Success)
            {
                await nav.AlertAsync("Не вдалося створити ціль", result.Error ?? string.Empty);
                return;
            }
        }
        finally { IsBusy = false; }

        await nav.GoBackAsync();
    }
}
