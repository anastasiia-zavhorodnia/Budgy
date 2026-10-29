namespace Budgy.Models;

public enum BudgetMode { Personal, Household }
public enum ThemeMode { System, Light, Dark }
public enum AnalyticsPeriod { Week, Month, Year }

/// <summary>Результат бізнес-операції (замість винятків для очікуваних помилок валідації).</summary>
public record OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

/// <summary>Фільтр списку транзакцій.</summary>
public class TransactionFilter
{
    public TransactionType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? CategoryId { get; set; }
    public int? AccountId { get; set; }
    public int? MemberId { get; set; }
    public string? Search { get; set; }
}

/// <summary>Група транзакцій за датою ("Сьогодні", "Вчора", ...).</summary>
public class TransactionGroup : List<Transaction>
{
    public string Name { get; }
    public TransactionGroup(string name, IEnumerable<Transaction> items) : base(items) => Name = name;
}

public class CategoryStat
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public decimal Amount { get; set; }
    public double Percent { get; set; } // 0..1
    public string ColorHex { get; set; } = "#5B3FD9"; // колір сектора діаграми та легенди
}

/// <summary>Палітра кольорів для діаграми (циклічно за кількістю категорій).</summary>
public static class ChartPalette
{
    public static readonly string[] Colors =
    {
        "#5B3FD9", "#1FA971", "#F59E0B", "#E5484D", "#3B82F6",
        "#EC4899", "#14B8A6", "#8B5CF6", "#F97316", "#64748B"
    };
}

public class DynamicsPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class AnalyticsSummary
{
    public AnalyticsPeriod Period { get; set; }
    public string CurrentTitle { get; set; } = string.Empty;
    public string PreviousTitle { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal PreviousExpense { get; set; }
    public List<CategoryStat> Categories { get; set; } = new();
    public List<DynamicsPoint> Dynamics { get; set; } = new();

    /// <summary>Економія відносно попереднього періоду (додатна – витратили менше).</summary>
    public decimal Savings => PreviousExpense - TotalExpense;
    public double SavingsPercent => PreviousExpense <= 0 ? 0 : (double)(Savings / PreviousExpense);
}
