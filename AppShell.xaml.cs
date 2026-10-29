using Budgy.Views;

namespace Budgy;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Екрани, які відкриваються поверх основної навігації
        Routing.RegisterRoute("AccountDetails", typeof(AccountDetailsPage));
        Routing.RegisterRoute("AddTransaction", typeof(AddTransactionPage));
        Routing.RegisterRoute("AddGoal", typeof(AddGoalPage));
    }
}