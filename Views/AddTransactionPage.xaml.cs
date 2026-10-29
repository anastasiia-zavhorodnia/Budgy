using Budgy.ViewModels;

namespace Budgy.Views;

public partial class AddTransactionPage : ContentPage
{
    private readonly AddTransactionViewModel _viewModel;

    public AddTransactionPage(AddTransactionViewModel viewModel)
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