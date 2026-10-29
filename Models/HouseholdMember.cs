using SQLite;

namespace Budgy.Models;

/// <summary>Член сім'ї, який бере участь у витратах.</summary>
[Table("HouseholdMembers")]
public class HouseholdMember
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public FamilyRole Role { get; set; } = FamilyRole.Other;

    [Indexed, NotNull]
    public int HouseholdId { get; set; }

    [Ignore] public Household? Household { get; set; }
    [Ignore] public List<Transaction> Transactions { get; set; } = new();
}
