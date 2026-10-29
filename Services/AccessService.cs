using Budgy.Models;
using Microsoft.Extensions.Logging;

namespace Budgy.Services;

public static class AccessMessages
{
    public const string Denied = "Недостатньо прав для цієї операції. Зверніться до батьків.";
}

/// <summary>
/// Рольовий доступ (лаб. №6). Права визначаються роллю ПОТОЧНОГО профілю (ISettingsService.CurrentMemberId),
/// тому після перемикання профілю на екрані «Сім'я» вони змінюються одразу.
/// Якщо профіль не обрано (порожня база / початкове налаштування) — обмежень немає.
/// </summary>
public interface IAccessService
{
    Task<HouseholdMember?> GetCurrentMemberAsync();
    Task<bool> IsChildAsync();
    /// <summary>У режимі «Особисті» повертає Id поточного профілю, інакше null (усі члени сім'ї).</summary>
    Task<int?> GetActiveMemberFilterAsync();
    /// <summary>Дія лише для дорослих: Дитині повертає OperationResult.Fail(AccessMessages.Denied).</summary>
    Task<OperationResult> RequireAdultAsync();
    /// <summary>Доросла людина може все; Дитина – лише з власними записами.</summary>
    Task<OperationResult> RequireAdultOrOwnerAsync(int ownerMemberId);
}

public class AccessService(FinanceDataStore store, ISettingsService settings, ILogger<AccessService> logger) : IAccessService
{
    public async Task<HouseholdMember?> GetCurrentMemberAsync()
    {
        var id = settings.CurrentMemberId;
        if (id <= 0) return null;
        var db = await store.GetConnectionAsync();
        return await db.FindAsync<HouseholdMember>(id);
    }

    public async Task<bool> IsChildAsync() => (await GetCurrentMemberAsync())?.Role == FamilyRole.Child;

    public async Task<int?> GetActiveMemberFilterAsync()
    {
        if (settings.BudgetMode != BudgetMode.Personal) return null;
        return (await GetCurrentMemberAsync())?.Id;
    }

    public async Task<OperationResult> RequireAdultAsync()
    {
        try
        {
            var member = await GetCurrentMemberAsync();
            if (member is null || member.Role != FamilyRole.Child) return OperationResult.Ok();

            logger.LogWarning("Відмовлено в доступі: профіль «{Member}» (роль {Role}) спробував дію, що потребує прав дорослого",
                member.Name, member.Role);
            return OperationResult.Fail(AccessMessages.Denied);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося перевірити права доступу");
            return OperationResult.Fail("Не вдалося перевірити права доступу. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> RequireAdultOrOwnerAsync(int ownerMemberId)
    {
        try
        {
            var member = await GetCurrentMemberAsync();
            if (member is null || member.Role != FamilyRole.Child || member.Id == ownerMemberId)
                return OperationResult.Ok();

            logger.LogWarning("Відмовлено в доступі: профіль «{Member}» (Дитина) спробував змінити чужий запис (власник {Owner})",
                member.Name, ownerMemberId);
            return OperationResult.Fail(AccessMessages.Denied);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося перевірити права доступу");
            return OperationResult.Fail("Не вдалося перевірити права доступу. Спробуйте ще раз.");
        }
    }
}