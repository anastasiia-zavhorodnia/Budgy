using SQLite;

namespace Budgy.Models;

/// <summary>Домогосподарство (сім'я).</summary>
[Table("Households")]
public class Household
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Ignore]
    public List<HouseholdMember> Members { get; set; } = new();
}
