using Budgy.ViewModels;
using Budgy.Views;

namespace Budgy.Services;

public static class ServiceRegistration
{
    /// <summary>Реєстрація сервісів, ViewModel та сторінок. Викликати в MauiProgram: builder.Services.AddFinanceApp();</summary>
    public static IServiceCollection AddFinanceApp(this IServiceCollection s)
    {
        // Services (singleton – одна БД і спільні налаштування для всього додатка)
        s.AddSingleton<FinanceDataStore>();
        s.AddSingleton<ISettingsService, SettingsService>();
        s.AddSingleton<INavigationService, NavigationService>();
        s.AddSingleton<IAccessService, AccessService>();
        s.AddSingleton<IAccountService, AccountService>();
        s.AddSingleton<ITransactionService, TransactionService>();
        s.AddSingleton<ICategoryService, CategoryService>();
        s.AddSingleton<IHouseholdService, HouseholdService>();
        s.AddSingleton<ISavingsGoalService, SavingsGoalService>();
        s.AddSingleton<IAnalyticsService, AnalyticsService>();
        s.AddSingleton<IReceiptService, ReceiptService>();
        s.AddSingleton<IMerchantService, MerchantService>();

        // ViewModels
        s.AddSingleton<MainViewModel>();
        s.AddTransient<DashboardViewModel>();
        s.AddTransient<AccountsViewModel>();
        s.AddTransient<AccountDetailsViewModel>();
        s.AddTransient<TransactionsViewModel>();
        s.AddTransient<AddTransactionViewModel>();
        s.AddTransient<CategoriesViewModel>();
        s.AddTransient<HouseholdViewModel>();
        s.AddTransient<SavingsGoalsViewModel>();
        s.AddTransient<AddSavingsGoalViewModel>();
        s.AddTransient<AnalyticsViewModel>();
        s.AddTransient<SettingsViewModel>();

        // Pages
        s.AddTransient<DashboardPage>();
        s.AddTransient<AccountsPage>();
        s.AddTransient<AccountDetailsPage>();
        s.AddTransient<TransactionsPage>();
        s.AddTransient<AddTransactionPage>();
        s.AddTransient<CategoriesPage>();
        s.AddTransient<HouseholdPage>();
        s.AddTransient<SavingsGoalsPage>();
        s.AddTransient<AddGoalPage>();
        s.AddTransient<AnalyticsPage>();
        s.AddTransient<SettingsPage>();

        return s;
    }
}
