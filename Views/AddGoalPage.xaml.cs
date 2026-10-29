using Budgy.ViewModels;

namespace Budgy.Views;

public partial class AddGoalPage : ContentPage
{
    private readonly AddSavingsGoalViewModel _viewModel;

    public AddGoalPage(AddSavingsGoalViewModel viewModel)
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