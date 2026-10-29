using SQLite;

namespace Budgy.Models;

/// <summary>Інформація про рахунок користувача.</summary>
[Table("Accounts")]
public class Account
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    public decimal Balance { get; set; }
    public Currency Currency { get; set; } = Currency.UAH;
    public AccountType AccountType { get; set; } = AccountType.Card;
    public bool IsActive { get; set; } = true;

    [Ignore]
    public List<Transaction> Transactions { get; set; } = new();
}
