using Budgy.ViewModels;

namespace Budgy.Views;

public partial class AccountDetailsPage : ContentPage
{
    private readonly AccountDetailsViewModel _viewModel;

    public AccountDetailsPage(AccountDetailsViewModel viewModel)
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