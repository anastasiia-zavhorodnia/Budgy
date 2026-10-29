using Budgy.ViewModels;

namespace Budgy.Views;

public partial class SavingsGoalsPage : ContentPage
{
    private readonly SavingsGoalsViewModel _viewModel;

    public SavingsGoalsPage(SavingsGoalsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}