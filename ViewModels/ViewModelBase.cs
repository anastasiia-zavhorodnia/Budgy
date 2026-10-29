using System.Runtime.CompilerServices;
using Budgy.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Budgy.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// Викликається сторінкою в OnAppearing. Помилка завантаження логуватиметься, а не "покладе" додаток.
    /// Конкретні ViewModel перевизначають LoadCoreAsync.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            await LoadCoreAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, $"{GetType().Name}.LoadAsync");
        }
    }

    protected virtual Task LoadCoreAsync() => Task.CompletedTask;

    /// <summary>Запускає async-роботу з обробників подій (partial-хуки), логуючи будь-який виняток.</summary>
    protected async void RunSafe(Func<Task> work, [CallerMemberName] string? context = null)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, $"{GetType().Name}.{context}");
        }
    }
}
