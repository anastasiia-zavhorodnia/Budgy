using System.Globalization;

namespace Budgy.Services;

public static class Input
{
    /// <summary>Парсить суму ("1 500,50" / "1500.5"); повертає true лише для значення &gt; 0.</summary>
    public static bool TryParseAmount(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0;
    }
}

public static class NavigationExtensions
{
    public static async Task<T?> PickAsync<T>(this INavigationService nav, string title,
        IEnumerable<T> items, Func<T, string> label) where T : class
    {
        var list = items.ToList();
        var labels = list.Select(label).ToArray();
        var choice = await nav.ActionSheetAsync(title, "Скасувати", null, labels);
        var i = choice is null ? -1 : Array.IndexOf(labels, choice);
        return i >= 0 ? list[i] : null;
    }

    public static async Task<T?> PickValueAsync<T>(this INavigationService nav, string title,
        IEnumerable<T> items, Func<T, string> label) where T : struct
    {
        var list = items.ToList();
        var labels = list.Select(label).ToArray();
        var choice = await nav.ActionSheetAsync(title, "Скасувати", null, labels);
        var i = choice is null ? -1 : Array.IndexOf(labels, choice);
        return i >= 0 ? list[i] : null;
    }
}
