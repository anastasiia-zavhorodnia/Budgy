using Budgy.Models;

namespace Budgy.Services;

/// <summary>Довідник популярних українських місць покупки.</summary>
public interface IMerchantService
{
    IReadOnlyList<string> Groups { get; }
    IReadOnlyList<MerchantInfo> GetAll();
    IReadOnlyList<MerchantInfo> GetByGroup(string group);
    /// <summary>Підказки під час введення: порожній текст → популярні, інакше збіги за назвою (спершу ті, що починаються з тексту).</summary>
    IReadOnlyList<MerchantInfo> Search(string? text, int max = 10);
}

public class MerchantService : IMerchantService
{
    private const string Supermarkets = "Супермаркети";
    private const string Fuel = "АЗС";
    private const string Food = "Кафе та фастфуд";
    private const string Pharmacies = "Аптеки";
    private const string Shops = "Магазини та техніка";
    private const string Services = "Послуги та зв’язок";

    // Категорії з FinanceDataStore (базові категорії); збіг за назвою, регістр не важливий
    private const string Groceries = "Продукти";
    private const string Transport = "Транспорт";
    private const string Cafe = "Кафе";
    private const string Health = "Здоров'я";
    private const string Clothes = "Одяг";

    private static readonly IReadOnlyList<MerchantInfo> Catalog = new List<MerchantInfo>
    {
        // Супермаркети
        new("Сільпо",    "🛒", Supermarkets, Groceries, true),
        new("Фора",      "🥦", Supermarkets, Groceries, true),
        new("АТБ",       "🏪", Supermarkets, Groceries, true),
        new("Novus",     "🧺", Supermarkets, Groceries),
        new("Varus",     "🍎", Supermarkets, Groceries),
        new("Близенько", "🥖", Supermarkets, Groceries),
        new("Ашан",      "🏬", Supermarkets, Groceries),
        new("Metro",     "🥫", Supermarkets, Groceries),

        // АЗС
        new("OKKO",        "⛽", Fuel, Transport, true),
        new("WOG",         "⛽", Fuel, Transport, true),
        new("SOCAR",       "⛽", Fuel, Transport),
        new("UPG",         "⛽", Fuel, Transport),
        new("БРСМ-Нафта",  "⛽", Fuel, Transport),

        // Кафе та фастфуд
        new("Aroma Kava",          "☕", Food, Cafe, true),
        new("McDonald’s",          "🍔", Food, Cafe),
        new("KFC",                 "🍗", Food, Cafe),
        new("Челентано",           "🍕", Food, Cafe),
        new("Львівські круасани",  "🥐", Food, Cafe),
        new("Sushiya",             "🍣", Food, Cafe),

        // Аптеки
        new("Подорожник",         "💊", Pharmacies, Health, true),
        new("АНЦ",                "💊", Pharmacies, Health),
        new("Бажаємо здоров’я",   "💊", Pharmacies, Health),

        // Магазини та техніка
        new("Аврора",     "🛍️", Shops, null, true),
        new("Rozetka",    "💻", Shops, null, true),
        new("Епіцентр",   "🔨", Shops),
        new("Фокстрот",   "📱", Shops),
        new("Comfy",      "🔌", Shops),
        new("EVA",        "💄", Shops),
        new("Watsons",    "🧴", Shops),
        new("Intertop",   "👟", Shops, Clothes),
        new("LC Waikiki", "👕", Shops, Clothes),

        // Послуги та зв’язок
        new("Нова Пошта", "📦", Services, null, true),
        new("Укрпошта",   "📮", Services),
        new("Київстар",   "📶", Services),
        new("Vodafone",   "📶", Services),
        new("lifecell",   "📶", Services),
        new("Укрзалізниця","🚆", Services, Transport),
        new("Bolt",       "🚕", Services, Transport),
        new("Uber",       "🚕", Services, Transport),
    };

    private static readonly IReadOnlyList<string> GroupList = Catalog.Select(m => m.Group).Distinct().ToList();

    public IReadOnlyList<string> Groups => GroupList;

    public IReadOnlyList<MerchantInfo> GetAll() => Catalog;

    public IReadOnlyList<MerchantInfo> GetByGroup(string group)
        => Catalog.Where(m => m.Group == group).ToList();

    public IReadOnlyList<MerchantInfo> Search(string? text, int max = 10)
    {
        var query = text?.Trim();
        if (string.IsNullOrEmpty(query))
            return Catalog.Where(m => m.IsPopular).Take(max).ToList();

        var starts = Catalog.Where(m => m.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        var contains = Catalog.Where(m => m.Display.Contains(query, StringComparison.OrdinalIgnoreCase));
        return starts.Concat(contains).Distinct().Take(max).ToList();
    }
}
