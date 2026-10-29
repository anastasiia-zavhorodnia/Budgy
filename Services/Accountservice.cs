using Budgy.Models;
using Microsoft.Extensions.Logging;

namespace Budgy.Services;

public interface IAccountService
{
    Task<IReadOnlyList<Account>> GetAllAsync(bool activeOnly = false);
    Task<Account?> GetByIdAsync(int id);
    /// <summary>Сума балансів активних рахунків у вказаній валюті (без конвертації інших валют).</summary>
    Task<decimal> GetTotalBalanceAsync(Currency currency);
    Task<OperationResult> AddAsync(Account account);
    Task<OperationResult> UpdateAsync(Account account);
    /// <summary>Кількість операцій та внесків до цілей, які буде видалено разом з рахунком.</summary>
    Task<(int Transactions, int Contributions)> GetRelatedCountsAsync(int accountId);
    /// <summary>
    /// Каскадне видалення: рахунок разом з усіма його транзакціями та внесками до цілей
    /// (в одній транзакції БД). Лише для дорослих (Дитина отримує відмову).
    /// </summary>
    Task<OperationResult> DeleteAsync(int id);
    /// <summary>Ручне поповнення (коригування) балансу рахунку.</summary>
    Task<OperationResult> TopUpAsync(int accountId, decimal amount);
    Task<OperationResult> TransferAsync(int fromId, int toId, decimal amount);
}

public class AccountService(FinanceDataStore store, IAccessService access, ILogger<AccountService> logger) : IAccountService
{
    private const string SaveError = "Не вдалося зберегти зміни. Спробуйте ще раз.";

    public async Task<IReadOnlyList<Account>> GetAllAsync(bool activeOnly = false)
    {
        var db = await store.GetConnectionAsync();
        var list = await db.Table<Account>().ToListAsync();
        return list.Where(a => !activeOnly || a.IsActive).ToList();
    }

    public async Task<Account?> GetByIdAsync(int id)
    {
        var db = await store.GetConnectionAsync();
        return await db.FindAsync<Account>(id);
    }

    public async Task<decimal> GetTotalBalanceAsync(Currency currency)
    {
        var all = await GetAllAsync(activeOnly: true);
        return all.Where(a => a.Currency == currency).Sum(a => a.Balance);
    }

    public async Task<OperationResult> AddAsync(Account account)
    {
        try
        {
            var name = account.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return OperationResult.Fail("Вкажіть назву рахунку.");

            var db = await store.GetConnectionAsync();
            var all = await db.Table<Account>().ToListAsync();
            if (all.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Fail("Рахунок з такою назвою вже існує.");

            account.Id = 0;
            account.Name = name;
            await db.InsertAsync(account);
            logger.LogInformation("Створено рахунок {Id} «{Name}»", account.Id, account.Name);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося створити рахунок «{Name}»", account.Name);
            return OperationResult.Fail("Не вдалося створити рахунок. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> UpdateAsync(Account account)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var existing = await db.FindAsync<Account>(account.Id);
            if (existing is null) return OperationResult.Fail("Рахунок не знайдено.");

            var name = account.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return OperationResult.Fail("Вкажіть назву рахунку.");

            var all = await db.Table<Account>().ToListAsync();
            if (all.Any(a => a.Id != account.Id && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Fail("Рахунок з такою назвою вже існує.");

            existing.Name = name;
            existing.BankName = account.BankName;
            existing.AccountType = account.AccountType;
            existing.IsActive = account.IsActive;
            // Валюту та баланс напряму не змінюємо: баланс змінюється лише операціями.
            await db.UpdateAsync(existing);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося оновити рахунок {Id}", account.Id);
            return OperationResult.Fail(SaveError);
        }
    }

    public async Task<(int Transactions, int Contributions)> GetRelatedCountsAsync(int accountId)
    {
        var db = await store.GetConnectionAsync();
        var transactions = await db.Table<Transaction>().Where(t => t.AccountId == accountId).CountAsync();
        var contributions = await db.Table<SavingsContribution>().Where(x => x.AccountId == accountId).CountAsync();
        return (transactions, contributions);
    }

    public async Task<OperationResult> DeleteAsync(int id)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            var db = await store.GetConnectionAsync();
            var account = await db.FindAsync<Account>(id);
            if (account is null) return OperationResult.Fail("Рахунок не знайдено.");

            var transactions = await db.Table<Transaction>().Where(t => t.AccountId == id).ToListAsync();
            var contributions = await db.Table<SavingsContribution>().Where(x => x.AccountId == id).ToListAsync();
            var receiptPaths = transactions
                .Select(t => t.ImagePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            // Усе або нічого: внески, транзакції та сам рахунок видаляються в одній транзакції БД
            await db.RunInTransactionAsync(conn =>
            {
                // Суму внесків, зроблених з цього рахунку, прибираємо з відповідних цілей,
                // щоб накопичене в цілі завжди дорівнювало сумі її внесків.
                foreach (var byGoal in contributions.GroupBy(x => x.SavingsGoalId))
                {
                    var goal = conn.Find<SavingsGoal>(byGoal.Key);
                    if (goal is null) continue;

                    goal.CurrentAmount = Math.Max(0, goal.CurrentAmount - byGoal.Sum(x => x.Amount));
                    conn.Update(goal);
                }

                conn.Table<SavingsContribution>().Delete(x => x.AccountId == id);
                conn.Table<Transaction>().Delete(t => t.AccountId == id);
                conn.Delete(account);
            });

            // файли чеків видалених транзакцій (найкращі зусилля, після успішного коміту)
            foreach (var path in receiptPaths) TryDeleteFile(path);

            logger.LogInformation(
                "Видалено рахунок {Id} «{Name}» разом з {Tx} операціями та {Contrib} внесками",
                id, account.Name, transactions.Count, contributions.Count);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося видалити рахунок {Id}", id);
            return OperationResult.Fail("Не вдалося видалити рахунок. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> TopUpAsync(int accountId, decimal amount)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var account = await db.FindAsync<Account>(accountId);
            if (account is null) return OperationResult.Fail("Рахунок не знайдено.");
            if (!account.IsActive) return OperationResult.Fail("Рахунок неактивний.");
            if (amount <= 0) return OperationResult.Fail("Сума має бути більшою за нуль.");

            account.Balance += amount;
            await db.UpdateAsync(account);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося поповнити рахунок {Id} на {Amount}", accountId, amount);
            return OperationResult.Fail("Не вдалося поповнити рахунок. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> TransferAsync(int fromId, int toId, decimal amount)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var from = await db.FindAsync<Account>(fromId);
            var to = await db.FindAsync<Account>(toId);
            if (from is null || to is null) return OperationResult.Fail("Рахунок не знайдено.");
            if (from.Id == to.Id) return OperationResult.Fail("Оберіть різні рахунки.");
            if (!from.IsActive || !to.IsActive) return OperationResult.Fail("Обидва рахунки мають бути активними.");
            if (from.Currency != to.Currency) return OperationResult.Fail("Переказ можливий лише між рахунками однієї валюти.");
            if (amount <= 0) return OperationResult.Fail("Сума має бути більшою за нуль.");
            if (from.Balance < amount) return OperationResult.Fail("Недостатньо коштів на рахунку.");

            // обидва оновлення — в одній транзакції БД: або обидва, або жодного
            await db.RunInTransactionAsync(conn =>
            {
                from.Balance -= amount;
                to.Balance += amount;
                conn.Update(from);
                conn.Update(to);
            });
            logger.LogInformation("Переказ {Amount} з рахунку {From} на {To}", amount, fromId, toId);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося виконати переказ {From} -> {To} на {Amount}", fromId, toId, amount);
            return OperationResult.Fail("Не вдалося виконати переказ. Спробуйте ще раз.");
        }
    }

    private void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { logger.LogWarning(ex, "Не вдалося видалити файл чека {Path}", path); }
    }
}
