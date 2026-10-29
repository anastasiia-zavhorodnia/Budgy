using System.Collections.ObjectModel;
using Budgy.Models;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Budgy.ViewModels;

public partial class AnalyticsViewModel(IAnalyticsService analytics, IAccessService access) : ViewModelBase
{
    [ObservableProperty] private AnalyticsPeriod _period = AnalyticsPeriod.Month;
    [ObservableProperty] private decimal _totalExpense;
    [ObservableProperty] private decimal _totalIncome;
    [ObservableProperty] private ObservableCollection<CategoryStat> _categoryStats = new();
    [ObservableProperty] private ObservableCollection<DynamicsPoint> _dynamics = new();
    [ObservableProperty] private string _currentPeriodTitle = string.Empty;
    [ObservableProperty] private string _previousPeriodTitle = string.Empty;
    [ObservableProperty] private decimal _currentPeriodExpense;
    [ObservableProperty] private decimal _previousPeriodExpense;
    [ObservableProperty] private string _savingsText = string.Empty;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _isEmpty = true;

    /// <summary>Ключ активного сегмента (Week / Month / Year).</summary>
    public string PeriodKey => Period.ToString();

    partial void OnPeriodChanged(AnalyticsPeriod value) => OnPropertyChanged(nameof(PeriodKey));

    protected override async Task LoadCoreAsync()
    {
        var s = await analytics.GetSummaryAsync(Period, await access.GetActiveMemberFilterAsync());

        // кольори секторів діаграми та легенди
        for (var i = 0; i < s.Categories.Count; i++)
            s.Categories[i].ColorHex = ChartPalette.Colors[i % ChartPalette.Colors.Length];

        TotalExpense = s.TotalExpense;
        TotalIncome = s.TotalIncome;
        CategoryStats = new(s.Categories);
        Dynamics = new(s.Dynamics);
        HasData = s.Categories.Count > 0;
        IsEmpty = !HasData;
        CurrentPeriodTitle = s.CurrentTitle;
        PreviousPeriodTitle = s.PreviousTitle;
        CurrentPeriodExpense = s.TotalExpense;
        PreviousPeriodExpense = s.PreviousExpense;

        SavingsText = s.PreviousExpense <= 0
            ? "Недостатньо даних для порівняння"
            : s.Savings >= 0
                ? $"Економія: {s.Savings:N0} ₴ ({s.SavingsPercent:P0})"
                : $"Перевитрата: {Math.Abs(s.Savings):N0} ₴ ({Math.Abs(s.SavingsPercent):P0})";
    }

    [RelayCommand]
    private Task SetPeriod(string? period)
    {
        Period = Enum.TryParse<AnalyticsPeriod>(period, out var p) ? p : AnalyticsPeriod.Month;
        return LoadAsync();
    }
}
