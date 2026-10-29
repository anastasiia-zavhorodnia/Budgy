namespace Budgy.Models;

/// <summary>Місце покупки зі довідника (мережа магазинів, АЗС, аптек, сервіс тощо).</summary>
/// <param name="Name">Назва мережі.</param>
/// <param name="Icon">Впізнаваний емодзі-значок.</param>
/// <param name="Group">Група довідника (Супермаркети, АЗС, ...).</param>
/// <param name="CategoryHint">Назва категорії витрат, яку варто запропонувати (Сільпо → Продукти).</param>
/// <param name="IsPopular">Показувати у швидкому списку, поки користувач нічого не ввів.</param>
public record MerchantInfo(string Name, string Icon, string Group, string? CategoryHint = null, bool IsPopular = false)
{
    /// <summary>Те, що зберігається в операції та показується в списках: «🛒 Сільпо».</summary>
    public string Display => $"{Icon} {Name}";
}
