using SQLite;

namespace Budgy.Models;

/// <summary>Категорія витрат або доходів.</summary>
[Table("Categories")]
public class Category
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Indexed]
    public TransactionType Type { get; set; } = TransactionType.Expense;

    [MaxLength(50)]
    public string? Icon { get; set; } // емодзі, назва або шлях до іконки

    [Ignore]
    public List<Subcategory> Subcategories { get; set; } = new();
}
