namespace Budgy.Models;

/// <summary>Тип операції або категорії: витрата / дохід.</summary>
public enum TransactionType
{
    Expense = 0, // Витрата
    Income = 1   // Дохід
}

/// <summary>Тип рахунку.</summary>
public enum AccountType
{
    Card = 0,     // Банківська картка
    Cash = 1,     // Готівка
    Deposit = 2,  // Депозит
    Savings = 3,  // Накопичувальний
    Other = 4
}

/// <summary>Валюта рахунку.</summary>
public enum Currency
{
    UAH = 0,
    USD = 1,
    EUR = 2
}

/// <summary>Роль у сім'ї.</summary>
public enum FamilyRole
{
    Father = 0, // Батько
    Mother = 1, // Мати
    Child = 2,  // Дитина
    Other = 3
}