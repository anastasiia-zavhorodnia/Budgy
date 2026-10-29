using Budgy.Models;
using Microsoft.Extensions.Logging;
using SQLite;

namespace Budgy.Services;

public interface ITransactionService
{
    Task<IReadOnlyList<Transaction>> QueryAsync(TransactionFilter? filter = null);
    Task<Transaction?> GetByIdAsync(int id);
    Task<OperationResult> AddAsync(Transaction transaction);
    Task<OperationResult> UpdateAsync(Transaction transaction);
    Task<OperationResult> DeleteAsync(int id);
}

public class TransactionService(FinanceDataStore store, IAccessService access, ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<IReadOnlyList<Transaction>> QueryAsync(TransactionFilter? f = null)
    {
        var db = await store.GetConnectionAsync();
        var all = await db.Table<Transaction>().ToListAsync();
        var accounts = (await db.Table<Account>().ToListAsync()).ToDictionary(a => a.Id);
        var categories = (await db.Table<Category>().ToListAsync()).ToDictionary(c => c.Id);
        var subs = (await db.Table<Subcategory>().ToListAsync()).ToDictionary(s => s.Id);
        var members = (await db.Table<HouseholdMember>().ToListAsync()).ToDictionary(m => m.Id);

        IEnumerable<Transaction> q = all;
        if (f is not null)
        {
            if (f.Type is { } type) q = q.Where(t => t.Type == type);
            if (f.From is { } from) q = q.Where(t => t.Date.Date >= from.Date);
            if (f.To is { } to) q = q.Where(t => t.Date.Date <= to.Date);
            if (f.CategoryId is { } cid) q = q.Where(t => t.CategoryId == cid);
            if (f.AccountId is { } aid) q = q.Where(t => t.AccountId == aid);
            if (f.MemberId is { } mid) q = q.Where(t => t.HouseholdMemberId == mid);
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var s = f.Search.Trim();
                q = q.Where(t =>
                    Contains(t.Description, s) ||
                    Contains(t.Merchant, s) ||
                    Contains(categories.GetValueOrDefault(t.CategoryId)?.Name, s) ||
                    Contains(t.SubcategoryId is { } sid ? subs.GetValueOrDefault(sid)?.Name : null, s));
            }
        }

        var result = q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToList();
        foreach (var t in result)
        {
            t.Account = accounts.GetValueOrDefault(t.AccountId);
            t.Category = categories.GetValueOrDefault(t.CategoryId);
            t.Subcategory = t.SubcategoryId is { } sid ? subs.GetValueOrDefault(sid) : null;
            t.HouseholdMember = members.GetValueOrDefault(t.HouseholdMemberId);
        }
        return result;

        static bool Contains(string? value, string s)
            => value?.Contains(s, StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        var db = await store.GetConnectionAsync();
        var t = await db.FindAsync<Transaction>(id);
        if (t is null) return null;

        t.Account = await db.FindAsync<Account>(t.AccountId);
        t.Category = await db.FindAsync<Category>(t.CategoryId);
        t.Subcategory = t.SubcategoryId is { } sid ? await db.FindAsync<Subcategory>(sid) : null;
        t.HouseholdMember = await db.FindAsync<HouseholdMember>(t.HouseholdMemberId);
        return t;
    }

    public async Task<OperationResult> AddAsync(Transaction t)
    {
        try
        {
            // Дитина може додавати лише власні операції
            var gate = await access.RequireAdultOrOwnerAsync(t.HouseholdMemberId);
            if (!gate.Success) return gate;

            var db = await store.GetConnectionAsync();
            var check = await ValidateAsync(db, t, null);
            if (!check.Success) return check;

            t.Id = 0;
            t.Description = string.IsNullOrWhiteSpace(t.Description) ? null : t.Description.Trim();
            t.Merchant = string.IsNullOrWhiteSpace(t.Merchant) ? null : t.Merchant.Trim();

            // запис операції + зміна балансу — в одній транзакції БД
            await db.RunInTransactionAsync(conn =>
            {
                conn.Insert(t);
                var account = conn.Find<Account>(t.AccountId);
                Apply(account, t, +1);
                conn.Update(account);
            });
            logger.LogInformation("Додано операцію {Id}: {Type} {Amount}", t.Id, t.Type, t.Amount);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося додати операцію (сума {Amount}, рахунок {Account})", t.Amount, t.AccountId);
            return OperationResult.Fail("Не вдалося зберегти операцію. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> UpdateAsync(Transaction updated)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var existing = await db.FindAsync<Transaction>(updated.Id);
            if (existing is null) return OperationResult.Fail("Транзакцію не знайдено.");

            var gateOld = await access.RequireAdultOrOwnerAsync(existing.HouseholdMemberId);
            if (!gateOld.Success) return gateOld;
            var gateNew = await access.RequireAdultOrOwnerAsync(updated.HouseholdMemberId);
            if (!gateNew.Success) return gateNew;

            var check = await ValidateAsync(db, updated, existing);
            if (!check.Success) return check;

            await db.RunInTransactionAsync(conn =>
            {
                // 1) скасувати вплив старої версії
                var oldAccount = conn.Find<Account>(existing.AccountId);
                if (oldAccount is not null)
                {
                    Apply(oldAccount, existing, -1);
                    conn.Update(oldAccount);
                }

                // 2) оновити поля
                existing.Amount = updated.Amount;
                existing.Date = updated.Date;
                existing.Type = updated.Type;
                existing.AccountId = updated.AccountId;
                existing.CategoryId = updated.CategoryId;
                existing.SubcategoryId = updated.SubcategoryId;
                existing.HouseholdMemberId = updated.HouseholdMemberId;
                existing.Description = string.IsNullOrWhiteSpace(updated.Description) ? null : updated.Description.Trim();
                existing.Merchant = string.IsNullOrWhiteSpace(updated.Merchant) ? null : updated.Merchant.Trim();
                existing.ImagePath = updated.ImagePath;
                existing.Latitude = updated.Latitude;
                existing.Longitude = updated.Longitude;
                conn.Update(existing);

                // 3) застосувати нову версію (рахунок перечитуємо, щоб не втратити зміну з кроку 1)
                var newAccount = conn.Find<Account>(existing.AccountId);
                Apply(newAccount, existing, +1);
                conn.Update(newAccount);
            });
            logger.LogInformation("Оновлено операцію {Id}", existing.Id);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося оновити операцію {Id}", updated.Id);
            return OperationResult.Fail("Не вдалося зберегти зміни. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> DeleteAsync(int id)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var existing = await db.FindAsync<Transaction>(id);
            if (existing is null) return OperationResult.Fail("Транзакцію не знайдено.");

            // Дитина може видаляти лише власні операції
            var gate = await access.RequireAdultOrOwnerAsync(existing.HouseholdMemberId);
            if (!gate.Success) return gate;

            await db.RunInTransactionAsync(conn =>
            {
                var account = conn.Find<Account>(existing.AccountId);
                if (account is not null)
                {
                    Apply(account, existing, -1); // повернути баланс
                    conn.Update(account);
                }
                conn.Delete(existing);
            });
            TryDeleteReceipt(existing.ImagePath);
            logger.LogInformation("Видалено операцію {Id}", id);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося видалити операцію {Id}", id);
            return OperationResult.Fail("Не вдалося видалити операцію. Спробуйте ще раз.");
        }
    }

    // ---------- helpers ----------

    private void TryDeleteReceipt(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { logger.LogWarning(ex, "Не вдалося видалити файл чека {Path}", path); }
    }

    /// <summary>Дохід збільшує баланс, витрата – зменшує. sign = -1 скасовує дію.</summary>
    private static void Apply(Account account, Transaction t, int sign)
        => account.Balance += sign * (t.Type == TransactionType.Income ? t.Amount : -t.Amount);

    private static async Task<OperationResult> ValidateAsync(SQLiteAsyncConnection db, Transaction t, Transaction? existing)
    {
        if (t.Amount <= 0) return OperationResult.Fail("Сума має бути більшою за нуль.");

        var account = await db.FindAsync<Account>(t.AccountId);
        if (account is null) return OperationResult.Fail("Оберіть рахунок.");
        // операцію на неактивному рахунку можна редагувати лише без зміни рахунку
        if (!account.IsActive && existing?.AccountId != account.Id)
            return OperationResult.Fail("Рахунок неактивний.");

        var category = await db.FindAsync<Category>(t.CategoryId);
        if (category is null) return OperationResult.Fail("Оберіть категорію.");
        if (category.Type != t.Type) return OperationResult.Fail("Тип категорії не збігається з типом операції.");

        if (t.SubcategoryId is { } sid)
        {
            var sub = await db.FindAsync<Subcategory>(sid);
            if (sub is null || sub.CategoryId != category.Id)
                return OperationResult.Fail("Підкатегорія не належить обраній категорії.");
        }

        if (await db.FindAsync<HouseholdMember>(t.HouseholdMemberId) is null)
            return OperationResult.Fail("Оберіть, хто здійснив операцію.");

        return OperationResult.Ok();
    }
}
