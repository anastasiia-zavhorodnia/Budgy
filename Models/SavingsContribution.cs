using SQLite;

namespace Budgy.Models;

/// <summary>Поповнення (або зняття, якщо сума від'ємна) фінансової цілі.</summary>
[Table("SavingsContributions")]
public class SavingsContribution
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, NotNull]
    public int SavingsGoalId { get; set; } // до якої цілі

    [Indexed, NotNull]
    public int AccountId { get; set; }     // з якого рахунку

    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;

    [Ignore] public SavingsGoal? SavingsGoal { get; set; }
    [Ignore] public Account? Account { get; set; }
}
