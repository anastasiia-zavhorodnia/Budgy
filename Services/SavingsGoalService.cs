using Budgy.Models;
using Microsoft.Extensions.Logging;

namespace Budgy.Services;

public interface ISavingsGoalService
{
    Task<IReadOnlyList<SavingsGoal>> GetAllAsync();
    Task<SavingsGoal?> GetByIdAsync(int id);
    Task<IReadOnlyList<SavingsContribution>> GetContributionsAsync(int goalId);
    Task<OperationResult> AddAsync(SavingsGoal goal);
    Task<OperationResult> UpdateAsync(SavingsGoal goal);
    /// <summary>Лише для дорослих.</summary>
    Task<OperationResult> DeleteAsync(int id);
    /// <summary>Поповнення цілі з рахунку (гроші списуються з рахунку).</summary>
    Task<OperationResult> ContributeAsync(int goalId, int accountId, decimal amount);
    /// <summary>Зняття з цілі на рахунок (фіксується як поповнення з від'ємною сумою).</summary>
    Task<OperationResult> WithdrawAsync(int goalId, int accountId, decimal amount);
}

public class SavingsGoalService(
    FinanceDataStore store,
    IAccessService access,
    ISettingsService settings,
    ILogger<SavingsGoalService> logger) : ISavingsGoalService
{
    public async Task<IReadOnlyList<SavingsGoal>> GetAllAsync()
    {
        var db = await store.GetConnectionAsync();
        var goals = await db.Table<SavingsGoal>().ToListAsync();
        return goals.OrderBy(g => g.IsCompleted).ThenBy(g => g.Deadline ?? DateTime.MaxValue).ToList();
    }

    public async Task<SavingsGoal?> GetByIdAsync(int id)
    {
        var db = await store.GetConnectionAsync();
        return await db.FindAsync<SavingsGoal>(id);
    }

    public async Task<IReadOnlyList<SavingsContribution>> GetContributionsAsync(int goalId)
    {
        var db = await store.GetConnectionAsync();
        var list = await db.Table<SavingsContribution>().Where(c => c.SavingsGoalId == goalId).ToListAsync();
        return list.OrderByDescending(c => c.Date).ToList();
    }

    public async Task<OperationResult> AddAsync(SavingsGoal goal)
    {
        try
        {
            var check = Validate(goal);
            if (!check.Success) return check;

            var db = await store.GetConnectionAsync();
            goal.Id = 0;
            goal.Name = goal.Name.Trim();
            goal.CurrentAmount = 0;
            await db.InsertAsync(goal);
            logger.LogInformation("Створено ціль {Id} «{Name}» на {Target}", goal.Id, goal.Name, goal.TargetAmount);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося створити ціль «{Name}»", goal.Name);
            return OperationResult.Fail("Не вдалося створити ціль. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> UpdateAsync(SavingsGoal goal)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var existing = await db.FindAsync<SavingsGoal>(goal.Id);
            if (existing is null) return OperationResult.Fail("Ціль не знайдено.");

            var check = Validate(goal);
            if (!check.Success) return check;
            if (goal.TargetAmount < existing.CurrentAmount)
                return OperationResult.Fail("Цільова сума не може бути меншою за вже накопичену.");

            existing.Name = goal.Name.Trim();
            existing.TargetAmount = goal.TargetAmount;
            existing.Deadline = goal.Deadline;
            existing.Description = goal.Description;
            await db.UpdateAsync(existing);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося оновити ціль {Id}", goal.Id);
            return OperationResult.Fail("Не вдалося зберегти зміни. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> DeleteAsync(int id)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            var db = await store.GetConnectionAsync();
            var goal = await db.FindAsync<SavingsGoal>(id);
            if (goal is null) return OperationResult.Fail("Ціль не знайдено.");
            if (goal.CurrentAmount > 0)
                return OperationResult.Fail("Спочатку зніміть накопичені кошти з цілі на рахунок.");

            await db.RunInTransactionAsync(conn =>
            {
                conn.Table<SavingsContribution>().Delete(c => c.SavingsGoalId == id);
                conn.Delete(goal);
            });
            logger.LogInformation("Видалено ціль {Id} «{Name}»", id, goal.Name);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося видалити ціль {Id}", id);
            return OperationResult.Fail("Не вдалося видалити ціль. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> ContributeAsync(int goalId, int accountId, decimal amount)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var goal = await db.FindAsync<SavingsGoal>(goalId);
            var account = await db.FindAsync<Account>(accountId);
            if (goal is null) return OperationResult.Fail("Ціль не знайдено.");
            if (account is null) return OperationResult.Fail("Оберіть рахунок.");
            if (!account.IsActive) return OperationResult.Fail("Рахунок неактивний.");
            if (account.Currency != settings.Currency)
                return OperationResult.Fail($"Цілі ведуться у валюті {settings.Currency}. Оберіть рахунок у цій валюті.");
            if (amount <= 0) return OperationResult.Fail("Сума має бути більшою за нуль.");
            if (goal.IsCompleted) return OperationResult.Fail("Ціль уже досягнуто.");
            if (amount > goal.Remaining) return OperationResult.Fail($"Сума перевищує залишок до цілі ({goal.Remaining:N0}).");
            if (account.Balance < amount) return OperationResult.Fail("Недостатньо коштів на рахунку.");

            await db.RunInTransactionAsync(conn =>
            {
                account.Balance -= amount;
                goal.CurrentAmount += amount;
                conn.Update(account);
                conn.Update(goal);
                conn.Insert(new SavingsContribution
                {
                    SavingsGoalId = goal.Id, AccountId = account.Id, Amount = amount, Date = DateTime.Now
                });
            });
            logger.LogInformation("Поповнення цілі {Goal} на {Amount} з рахунку {Account}", goalId, amount, accountId);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося поповнити ціль {Goal} з рахунку {Account} на {Amount}", goalId, accountId, amount);
            return OperationResult.Fail("Не вдалося поповнити ціль. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> WithdrawAsync(int goalId, int accountId, decimal amount)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var goal = await db.FindAsync<SavingsGoal>(goalId);
            var account = await db.FindAsync<Account>(accountId);
            if (goal is null) return OperationResult.Fail("Ціль не знайдено.");
            if (account is null) return OperationResult.Fail("Оберіть рахунок.");
            if (!account.IsActive) return OperationResult.Fail("Рахунок неактивний.");
            if (account.Currency != settings.Currency)
                return OperationResult.Fail($"Оберіть рахунок у валюті {settings.Currency}.");
            if (amount <= 0) return OperationResult.Fail("Сума має бути більшою за нуль.");
            if (amount > goal.CurrentAmount) return OperationResult.Fail("На цілі немає стільки коштів.");

            await db.RunInTransactionAsync(conn =>
            {
                goal.CurrentAmount -= amount;
                account.Balance += amount;
                conn.Update(goal);
                conn.Update(account);
                conn.Insert(new SavingsContribution
                {
                    SavingsGoalId = goal.Id, AccountId = account.Id, Amount = -amount, Date = DateTime.Now
                });
            });
            logger.LogInformation("Зняття з цілі {Goal} {Amount} на рахунок {Account}", goalId, amount, accountId);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося зняти кошти з цілі {Goal} на рахунок {Account}", goalId, accountId);
            return OperationResult.Fail("Не вдалося зняти кошти. Спробуйте ще раз.");
        }
    }

    private static OperationResult Validate(SavingsGoal g)
    {
        if (string.IsNullOrWhiteSpace(g.Name)) return OperationResult.Fail("Вкажіть назву цілі.");
        if (g.TargetAmount <= 0) return OperationResult.Fail("Цільова сума має бути більшою за нуль.");
        if (g.Deadline is { } d && d.Date < DateTime.Today) return OperationResult.Fail("Дедлайн не може бути в минулому.");
        return OperationResult.Ok();
    }
}
