using System.Globalization;
using Budgy.Models;

namespace Budgy.Services;

public interface IAnalyticsService
{
    Task<(decimal Income, decimal Expense)> GetTotalsAsync(DateTime from, DateTime to, int? memberId = null);
    Task<AnalyticsSummary> GetSummaryAsync(AnalyticsPeriod period, int? memberId = null);
}

public class AnalyticsService(FinanceDataStore store, ISettingsService settings) : IAnalyticsService
{
    private static readonly CultureInfo Uk = new("uk-UA");

    private sealed record Data(List<Transaction> Transactions, HashSet<int> BaseAccountIds, Dictionary<int, Category> Categories);

    public async Task<(decimal Income, decimal Expense)> GetTotalsAsync(DateTime from, DateTime to, int? memberId = null)
        => Totals(await LoadAsync(), from, to, memberId);

    public async Task<AnalyticsSummary> GetSummaryAsync(AnalyticsPeriod period, int? memberId = null)
    {
        var data = await LoadAsync();
        var (from, to, prevFrom, prevTo, title, prevTitle) = GetRanges(period, DateTime.Today);

        var current = InRange(data, from, to, memberId).ToList();
        var expenses = current.Where(t => t.Type == TransactionType.Expense).ToList();
        var totalExpense = expenses.Sum(t => t.Amount);

        var summary = new AnalyticsSummary
        {
            Period = period,
            CurrentTitle = title,
            PreviousTitle = prevTitle,
            TotalIncome = current.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
            TotalExpense = totalExpense,
            PreviousExpense = Totals(data, prevFrom, prevTo, memberId).Expense
        };

        summary.Categories = expenses
            .GroupBy(t => t.CategoryId)
            .Select(g =>
            {
                var cat = data.Categories.GetValueOrDefault(g.Key);
                var amount = g.Sum(t => t.Amount);
                return new CategoryStat
                {
                    Name = cat?.Name ?? "Без категорії",
                    Icon = cat?.Icon,
                    Amount = amount,
                    Percent = totalExpense <= 0 ? 0 : (double)(amount / totalExpense)
                };
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        summary.Dynamics = period == AnalyticsPeriod.Year
            ? expenses.GroupBy(t => t.Date.Month).OrderBy(g => g.Key)
                .Select(g => new DynamicsPoint { Label = Uk.DateTimeFormat.GetAbbreviatedMonthName(g.Key), Amount = g.Sum(t => t.Amount) }).ToList()
            : expenses.GroupBy(t => t.Date.Date).OrderBy(g => g.Key)
                .Select(g => new DynamicsPoint { Label = g.Key.ToString("dd.MM"), Amount = g.Sum(t => t.Amount) }).ToList();

        return summary;
    }

    private async Task<Data> LoadAsync()
    {
        var db = await store.GetConnectionAsync();
        var transactions = await db.Table<Transaction>().ToListAsync();
        var accounts = await db.Table<Account>().ToListAsync();
        var categories = (await db.Table<Category>().ToListAsync()).ToDictionary(c => c.Id);
        var baseIds = accounts.Where(a => a.Currency == settings.Currency).Select(a => a.Id).ToHashSet();
        return new Data(transactions, baseIds, categories);
    }

    private static (decimal Income, decimal Expense) Totals(Data data, DateTime from, DateTime to, int? memberId)
    {
        var list = InRange(data, from, to, memberId).ToList();
        return (list.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                list.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount));
    }

    /// <summary>Операції за період; рахуються лише рахунки в базовій валюті (без конвертації).</summary>
    private static IEnumerable<Transaction> InRange(Data data, DateTime from, DateTime to, int? memberId)
        => data.Transactions.Where(t =>
            t.Date.Date >= from.Date && t.Date.Date <= to.Date &&
            data.BaseAccountIds.Contains(t.AccountId) &&
            (memberId is null || t.HouseholdMemberId == memberId));

    private static (DateTime From, DateTime To, DateTime PrevFrom, DateTime PrevTo, string Title, string PrevTitle)
        GetRanges(AnalyticsPeriod period, DateTime today)
    {
        switch (period)
        {
            case AnalyticsPeriod.Week:
                var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // понеділок
                return (start, today, start.AddDays(-7), start.AddDays(-1), "Цей тиждень", "Минулий тиждень");
            case AnalyticsPeriod.Year:
                var y = new DateTime(today.Year, 1, 1);
                return (y, today, y.AddYears(-1), y.AddDays(-1), today.Year.ToString(), (today.Year - 1).ToString());
            default:
                var m = new DateTime(today.Year, today.Month, 1);
                var pm = m.AddMonths(-1);
                return (m, today, pm, m.AddDays(-1), MonthName(m), MonthName(pm));
        }

        static string MonthName(DateTime d)
        {
            var n = d.ToString("MMMM", Uk);
            return char.ToUpper(n[0], Uk) + n[1..];
        }
    }
}
