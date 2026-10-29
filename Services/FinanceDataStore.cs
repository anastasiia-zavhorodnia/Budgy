using Budgy.Models;
using Microsoft.Extensions.Logging;
using SQLite;

namespace Budgy.Services;

/// <summary>
/// Локальна база даних SQLite (sqlite-net-pcl).
/// При першій ініціалізації створює порожні таблиці, службове домогосподарство та набір базових категорій.
/// </summary>
public class FinanceDataStore(ILogger<FinanceDataStore> logger)
{
    public const string DatabaseFileName = "budgy.db3";
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    // Прапорець «базові категорії вже створювались»: якщо користувач потім видалить усі категорії,
    // вони не з'являться знову після перезапуску.
    private const string DefaultCategoriesSeededKey = "DefaultCategoriesSeeded";

    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly (string Name, string Icon, TransactionType Type, string[] Subcategories)[] DefaultCategories =
    {
        ("Продукти",           "🛒", TransactionType.Expense, new[] { "Супермаркет", "Ринок" }),
        ("Транспорт",          "🚗", TransactionType.Expense, new[] { "Пальне", "Громадський транспорт", "Таксі" }),
        ("Комунальні послуги", "🏠", TransactionType.Expense, new[] { "Електроенергія", "Вода", "Газ", "Інтернет" }),
        ("Кафе",               "☕", TransactionType.Expense, new[] { "Кава", "Ресторан", "Фастфуд" }),
        ("Розваги",            "🎉", TransactionType.Expense, new[] { "Кіно", "Відпочинок" }),
        ("Здоров'я",           "💊", TransactionType.Expense, new[] { "Аптека", "Лікар" }),
        ("Одяг",               "👕", TransactionType.Expense, new[] { "Одяг", "Взуття" }),
        ("Зарплата",           "💰", TransactionType.Income,  Array.Empty<string>()),
        ("Подарунки",          "🎁", TransactionType.Income,  Array.Empty<string>()),
        ("Кешбек",             "💸", TransactionType.Income,  Array.Empty<string>()),
    };

    /// <summary>Повертає з'єднання; під час першого виклику створює файл БД, таблиці та початкові дані.</summary>
    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null) return _connection;

        await _initLock.WaitAsync();
        try
        {
            if (_connection is not null) return _connection;

            var db = new SQLiteAsyncConnection(DatabasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await db.CreateTableAsync<Household>();
            await db.CreateTableAsync<HouseholdMember>();
            await db.CreateTableAsync<Account>();
            await db.CreateTableAsync<Category>();
            await db.CreateTableAsync<Subcategory>();
            await db.CreateTableAsync<Transaction>();
            await db.CreateTableAsync<SavingsGoal>();
            await db.CreateTableAsync<SavingsContribution>();

            // Службове порожнє домогосподарство (можна перейменувати на екрані «Сім'я»)
            if (await db.Table<Household>().CountAsync() == 0)
                await db.InsertAsync(new Household { Name = "Моє домогосподарство" });

            await SeedDefaultCategoriesAsync(db);

            _connection = db;
            logger.LogInformation("База даних ініціалізована: {Path}", DatabasePath);
            return db;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Не вдалося ініціалізувати базу даних {Path}", DatabasePath);
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Створює базові категорії лише один раз і лише якщо таблиця порожня
    /// (власні категорії користувача ніколи не перезаписуються).
    /// </summary>
    private async Task SeedDefaultCategoriesAsync(SQLiteAsyncConnection db)
    {
        if (Preferences.Default.Get(DefaultCategoriesSeededKey, false)) return;

        if (await db.Table<Category>().CountAsync() == 0)
        {
            await db.RunInTransactionAsync(conn =>
            {
                foreach (var (name, icon, type, subcategories) in DefaultCategories)
                {
                    var category = new Category { Name = name, Icon = icon, Type = type };
                    conn.Insert(category); // Id заповнюється після вставки

                    foreach (var sub in subcategories)
                        conn.Insert(new Subcategory { Name = sub, CategoryId = category.Id });
                }
            });
            logger.LogInformation("Створено {Count} базових категорій", DefaultCategories.Length);
        }

        Preferences.Default.Set(DefaultCategoriesSeededKey, true);
    }
}
