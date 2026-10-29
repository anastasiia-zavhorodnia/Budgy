using System.Globalization;
using Budgy.Models;

namespace Budgy.Converters;

/// <summary>Transaction -> "-850 ₴" / "+30 000 ₴".</summary>
public class SignedAmountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Transaction t) return string.Empty;
        var sign = t.Type == TransactionType.Income ? "+" : "-";
        return $"{sign}{t.Amount:N0} ₴";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Transaction -> зелений (дохід) / червоний (витрата).</summary>
public class TransactionColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var income = value is Transaction t && t.Type == TransactionType.Income;
        var key = income ? "Income" : "Expense";
        return Application.Current!.Resources.TryGetValue(key, out var c) ? c : Colors.Gray;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}