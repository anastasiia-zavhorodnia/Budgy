using SQLite;

namespace Budgy.Models;

/// <summary>Фінансова ціль (скарбничка).</summary>
[Table("SavingsGoals")]
public class SavingsGoal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? Deadline { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Ignore]
    public List<SavingsContribution> Contributions { get; set; } = new();

    // Обчислювані поля (не зберігаються в БД)
    [Ignore] public decimal Remaining => Math.Max(0, TargetAmount - CurrentAmount);
    [Ignore] public double Progress => TargetAmount <= 0 ? 0 : Math.Min(1.0, (double)(CurrentAmount / TargetAmount));
    [Ignore] public bool IsCompleted => CurrentAmount >= TargetAmount;
}
