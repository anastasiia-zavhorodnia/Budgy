using SQLite;

namespace Budgy.Models;

/// <summary>Одна фінансова операція (витрата або дохід).</summary>
[Table("Transactions")]
public class Transaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public TransactionType Type { get; set; } = TransactionType.Expense;

    [Indexed, NotNull]
    public int AccountId { get; set; }

    [Indexed, NotNull]
    public int CategoryId { get; set; }

    [Indexed]
    public int? SubcategoryId { get; set; }          // необов'язкова

    [Indexed, NotNull]
    public int HouseholdMemberId { get; set; }       // хто здійснив

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Місце покупки: вручну або з довідника, напр. «🛒 Сільпо».</summary>
    [MaxLength(100)]
    public string? Merchant { get; set; }

    // Фото чека та місце покупки (заповнюються на другому етапі: камера + геолокація)
    public string? ImagePath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Навігаційні властивості (не зберігаються в БД)
    [Ignore] public Account? Account { get; set; }
    [Ignore] public Category? Category { get; set; }
    [Ignore] public Subcategory? Subcategory { get; set; }
    [Ignore] public HouseholdMember? HouseholdMember { get; set; }

    /// <summary>Другий рядок у списках: «🛒 Сільпо · 12.10.2026» або лише дата.</summary>
    [Ignore]
    public string Subtitle => string.IsNullOrWhiteSpace(Merchant)
        ? Date.ToString("dd.MM.yyyy")
        : $"{Merchant} · {Date:dd.MM.yyyy}";
}
