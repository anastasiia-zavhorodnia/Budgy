using Budgy.Models;
using Microsoft.Extensions.Logging;

namespace Budgy.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(TransactionType? type = null);
    Task<Category?> GetByIdAsync(int id);
    Task<OperationResult> AddAsync(Category category);
    Task<OperationResult> UpdateAsync(Category category);
    /// <summary>Лише для дорослих.</summary>
    Task<OperationResult> DeleteAsync(int id);
    Task<OperationResult> AddSubcategoryAsync(int categoryId, string name);
    /// <summary>Лише для дорослих.</summary>
    Task<OperationResult> DeleteSubcategoryAsync(int subcategoryId);
}

public class CategoryService(FinanceDataStore store, IAccessService access, ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<IReadOnlyList<Category>> GetAllAsync(TransactionType? type = null)
    {
        var db = await store.GetConnectionAsync();
        var categories = await db.Table<Category>().ToListAsync();
        var subs = await db.Table<Subcategory>().ToListAsync();

        foreach (var c in categories)
            c.Subcategories = subs.Where(s => s.CategoryId == c.Id).ToList();

        return categories.Where(c => type is null || c.Type == type).ToList();
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        var db = await store.GetConnectionAsync();
        var category = await db.FindAsync<Category>(id);
        if (category is null) return null;
        category.Subcategories = await db.Table<Subcategory>().Where(s => s.CategoryId == id).ToListAsync();
        return category;
    }

    public async Task<OperationResult> AddAsync(Category category)
    {
        try
        {
            var name = category.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return OperationResult.Fail("Вкажіть назву категорії.");

            var db = await store.GetConnectionAsync();
            if (await ExistsAsync(db, name, category.Type, null))
                return OperationResult.Fail("Така категорія вже існує.");

            category.Id = 0;
            category.Name = name;
            await db.InsertAsync(category);
            logger.LogInformation("Створено категорію {Id} «{Name}»", category.Id, category.Name);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося створити категорію «{Name}»", category.Name);
            return OperationResult.Fail("Не вдалося створити категорію. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> UpdateAsync(Category category)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            var existing = await db.FindAsync<Category>(category.Id);
            if (existing is null) return OperationResult.Fail("Категорію не знайдено.");

            var name = category.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return OperationResult.Fail("Вкажіть назву категорії.");
            if (await ExistsAsync(db, name, existing.Type, existing.Id))
                return OperationResult.Fail("Така категорія вже існує.");

            existing.Name = name;
            existing.Icon = category.Icon; // тип категорії не змінюємо: існуючі операції прив'язані до нього
            await db.UpdateAsync(existing);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося оновити категорію {Id}", category.Id);
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
            var category = await db.FindAsync<Category>(id);
            if (category is null) return OperationResult.Fail("Категорію не знайдено.");

            if (await db.Table<Transaction>().Where(t => t.CategoryId == id).CountAsync() > 0)
                return OperationResult.Fail("Категорія використовується в операціях і не може бути видалена.");

            await db.RunInTransactionAsync(conn =>
            {
                conn.Table<Subcategory>().Delete(s => s.CategoryId == id);
                conn.Delete(category);
            });
            logger.LogInformation("Видалено категорію {Id} «{Name}»", id, category.Name);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося видалити категорію {Id}", id);
            return OperationResult.Fail("Не вдалося видалити категорію. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> AddSubcategoryAsync(int categoryId, string name)
    {
        try
        {
            var db = await store.GetConnectionAsync();
            if (await db.FindAsync<Category>(categoryId) is null) return OperationResult.Fail("Категорію не знайдено.");

            name = name?.Trim() ?? string.Empty;
            if (name.Length == 0) return OperationResult.Fail("Вкажіть назву підкатегорії.");

            var existing = await db.Table<Subcategory>().Where(s => s.CategoryId == categoryId).ToListAsync();
            if (existing.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Fail("Така підкатегорія вже існує.");

            await db.InsertAsync(new Subcategory { Name = name, CategoryId = categoryId });
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося додати підкатегорію «{Name}» до категорії {Id}", name, categoryId);
            return OperationResult.Fail("Не вдалося додати підкатегорію. Спробуйте ще раз.");
        }
    }

    public async Task<OperationResult> DeleteSubcategoryAsync(int subcategoryId)
    {
        try
        {
            var gate = await access.RequireAdultAsync();
            if (!gate.Success) return gate;

            var db = await store.GetConnectionAsync();
            var sub = await db.FindAsync<Subcategory>(subcategoryId);
            if (sub is null) return OperationResult.Fail("Підкатегорію не знайдено.");

            if (await db.Table<Transaction>().Where(t => t.SubcategoryId == subcategoryId).CountAsync() > 0)
                return OperationResult.Fail("Підкатегорія використовується в операціях.");

            await db.DeleteAsync(sub);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося видалити підкатегорію {Id}", subcategoryId);
            return OperationResult.Fail("Не вдалося видалити підкатегорію. Спробуйте ще раз.");
        }
    }

    private static async Task<bool> ExistsAsync(SQLite.SQLiteAsyncConnection db, string name, TransactionType type, int? exceptId)
    {
        var all = await db.Table<Category>().ToListAsync();
        return all.Any(c => c.Type == type && c.Id != exceptId && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
