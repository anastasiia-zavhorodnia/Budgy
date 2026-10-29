using Budgy.Models;
using Microsoft.Extensions.Logging;

namespace Budgy.Services;

public interface IHouseholdService
{
    Task<Household> GetHouseholdAsync();
    Task<IReadOnlyList<HouseholdMember>> GetMembersAsync();
    Task<HouseholdMember?> GetMemberAsync(int id);
    /// <summary>Лише для дорослих.</summary>
    Task<OperationResult> RenameAsync(string name);
    /// <summary>Лише для дорослих.</summary>
    Task<OperationResult> AddMemberAsync(HouseholdMember member);
    /// <summary>Лише для дорослих (зокрема зміна ролей).</summary>
    Task<OperationResult> UpdateMemberAsync(HouseholdMember member);
    /// <summary>Лише для дорослих; не можна видалити активний профіль.</summary>
    Task<OperationResult> RemoveMemberAsync(int memberId);
    /// <summary>Перемикає активний профіль (від нього залежать права доступу).</summary>
    Task<OperationResult> SetCurrentMemberAsync(int memberId);
}

public class HouseholdService(
    FinanceDataStore store,
    IAccessService access,
    ISettingsService settings,
    ILogger<HouseholdService> logger) : IHouseholdService
{
    public async Task<Household> GetHouseholdAsync()
    {
        var db = await store.GetConnectionAsync();
        var household = await db.Table<Household>().FirstOrDefaultAsync();
        if (household is null)
        {
            household = new Household { Name = "Моє домогосподарство" };
            await db.InsertAsync(household);
        }
        household.Members = await db.Table<HouseholdMember>().ToListAsync();
        return household;
    }

    public async Task<IReadOnlyList<HouseholdMember>> GetMembersAsync()
    {
        var db = await store.GetConnectionAsync();
        return await db.Table<HouseholdMember>().ToListAsync();
    }

    public async Task<HouseholdMember?> GetMemberAsync(int id)
    {
        var db = await store.GetConnectionAsync();
        return await db.FindAsync<HouseholdMember>(id);
    }

    public async Task<OperationResult> RenameAsync(string name)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            if (string.IsNullOrWhiteSpace(name)) return OperationResult.Fail("Вкажіть назву.");

            var household = await GetHouseholdAsync();
            household.Name = name.Trim();
            var db = await store.GetConnectionAsync();
            await db.UpdateAsync(household);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося перейменувати домогосподарство");
            return OperationResult.Fail("Не вдалося зберегти назву. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> AddMemberAsync(HouseholdMember member)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            var name = member.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return OperationResult.Fail("Вкажіть ім'я.");

            var db = await store.GetConnectionAsync();
            var members = await db.Table<HouseholdMember>().ToListAsync();
            if (members.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Fail("Член сім'ї з таким іменем вже є.");

            var household = await GetHouseholdAsync();
            member.Id = 0;
            member.Name = name;
            member.HouseholdId = household.Id;
            await db.InsertAsync(member);

            // Початкове налаштування: якщо активного профілю ще немає — робимо ним першого дорослого.
            // Якщо перший доданий — Дитина, профіль залишається не обраним, щоб вона не заблокувала налаштування.
            if (await access.GetCurrentMemberAsync() is null)
            {
                if (member.Role != FamilyRole.Child)
                {
                    settings.CurrentMemberId = member.Id;
                    settings.UserName = member.Name;
                }
                else
                {
                    settings.CurrentMemberId = 0;
                }
            }

            logger.LogInformation("Додано члена сім'ї {Id} «{Name}» ({Role})", member.Id, member.Name, member.Role);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося додати члена сім'ї «{Name}»", member.Name);
            return OperationResult.Fail("Не вдалося додати члена сім'ї. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> UpdateMemberAsync(HouseholdMember member)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            var db = await store.GetConnectionAsync();
            var existing = await db.FindAsync<HouseholdMember>(member.Id);
            if (existing is null) return OperationResult.Fail("Члена сім'ї не знайдено.");

            var name = member.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return OperationResult.Fail("Вкажіть ім'я.");

            var members = await db.Table<HouseholdMember>().ToListAsync();
            if (members.Any(m => m.Id != member.Id && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Fail("Член сім'ї з таким іменем вже є.");

            existing.Name = name;
            existing.Role = member.Role;
            await db.UpdateAsync(existing);

            if (existing.Id == settings.CurrentMemberId) settings.UserName = existing.Name;
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося оновити члена сім'ї {Id}", member.Id);
            return OperationResult.Fail("Не вдалося зберегти зміни. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> RemoveMemberAsync(int memberId)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            var db = await store.GetConnectionAsync();
            var member = await db.FindAsync<HouseholdMember>(memberId);
            if (member is null) return OperationResult.Fail("Члена сім'ї не знайдено.");

            if (member.Id == settings.CurrentMemberId)
                return OperationResult.Fail("Не можна видалити активний профіль. Спочатку перемкніться на інший.");

            if (await db.Table<Transaction>().Where(t => t.HouseholdMemberId == memberId).CountAsync() > 0)
                return OperationResult.Fail("У цього члена сім'ї є операції, тому видалити його не можна.");

            await db.DeleteAsync(member);
            logger.LogInformation("Видалено члена сім'ї {Id} «{Name}»", member.Id, member.Name);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося видалити члена сім'ї {Id}", memberId);
            return OperationResult.Fail("Не вдалося видалити члена сім'ї. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> SetCurrentMemberAsync(int memberId)
    {
        try
        {
            var member = await GetMemberAsync(memberId);
            if (member is null) return OperationResult.Fail("Члена сім'ї не знайдено.");

            settings.CurrentMemberId = member.Id;
            settings.UserName = member.Name;
            logger.LogInformation("Активний профіль: {Id} «{Name}» ({Role})", member.Id, member.Name, member.Role);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося перемкнути профіль на {Id}", memberId);
            return OperationResult.Fail("Не вдалося перемкнути профіль. Спробуйте ще раз.");
        }
    }
}
