using System.Globalization;
using Budgy.Models;

namespace Budgy.Converters;

public static class EnumText
{
    public static string Of(object? value) => value switch
    {
        FamilyRole.Father => "Батько",
        FamilyRole.Mother => "Мати",
        FamilyRole.Child => "Дитина",
        FamilyRole.Other => "Інше",
        AccountType.Card => "Картка",
        AccountType.Cash => "Готівка",
        AccountType.Deposit => "Депозит",
        AccountType.Savings => "Накопичувальний",
        AccountType.Other => "Інше",
        TransactionType.Expense => "Витрата",
        TransactionType.Income => "Дохід",
        ThemeMode.System => "Системна",
        ThemeMode.Light => "Світла",
        ThemeMode.Dark => "Темна",
        _ => value?.ToString() ?? string.Empty
    };
}

public class EnumTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => EnumText.Of(value);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
