namespace Budgy.Services;

public interface INavigationService
{
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);
    Task GoBackAsync();
    Task AlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> PromptAsync(string title, string message, string? initialValue = null, bool numeric = false);
    Task<string?> ActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons);
}

public class NavigationService : INavigationService
{
    private static Page CurrentPage => Shell.Current.CurrentPage ?? Shell.Current;

    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
        => parameters is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, new Dictionary<string, object>(parameters));

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");

    public Task AlertAsync(string title, string message) => CurrentPage.DisplayAlertAsync(title, message, "OK");

    public Task<bool> ConfirmAsync(string title, string message)
        => CurrentPage.DisplayAlertAsync(title, message, "Так", "Ні");

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null, bool numeric = false)
        => CurrentPage.DisplayPromptAsync(title, message, "OK", "Скасувати",
            keyboard: numeric ? Keyboard.Numeric : Keyboard.Default, initialValue: initialValue ?? string.Empty);

    public Task<string?> ActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
        => CurrentPage.DisplayActionSheetAsync(title, cancel, destruction, buttons)!;
}
