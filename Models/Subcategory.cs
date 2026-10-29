using SQLite;

namespace Budgy.Models;

/// <summary>Підкатегорія, яка належить певній категорії.</summary>
[Table("Subcategories")]
public class Subcategory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Indexed, NotNull]
    public int CategoryId { get; set; }

    [Ignore]
    public Category? Category { get; set; }
}
