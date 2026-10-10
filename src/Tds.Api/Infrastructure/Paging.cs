using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Tds.Api.Infrastructure;

/// <summary>Query string of every list endpoint (UI §5): ?page=1&pageSize=50&sort=-dueDate&q=tekst</summary>
public sealed record ListQuery(int? Page, int? PageSize, string? Sort, string? Q)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int SafePage => Math.Max(1, Page ?? 1);
    public int SafePageSize => Math.Clamp(PageSize ?? DefaultPageSize, 1, MaxPageSize);

    /// <summary>Trimmed search text, or null when empty.</summary>
    public string? Search => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Whitelisted sort keys per list: unknown keys fall back to the default, never to raw SQL.</summary>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _keys = new(StringComparer.OrdinalIgnoreCase);
    private string _defaultKey = "";
    private bool _defaultDescending;

    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        _keys[key] = (q, desc) => desc ? q.OrderByDescending(selector) : q.OrderBy(selector);
        return this;
    }

    public SortMap<T> Default(string key, bool descending = false)
    {
        _defaultKey = key;
        _defaultDescending = descending;
        return this;
    }

    /// <summary>"dueDate" ascending, "-dueDate" descending.</summary>
    public IOrderedQueryable<T> Apply(IQueryable<T> query, string? sort)
    {
        var desc = sort?.StartsWith('-') == true;
        var key = sort?.TrimStart('-');
        if (key is null || !_keys.TryGetValue(key, out var order))
            return _keys[_defaultKey](query, _defaultDescending);
        return order(query, desc);
    }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<TOut>> ToPagedAsync<T, TOut>(
        this IQueryable<T> query, ListQuery list, SortMap<T> sort, Expression<Func<T, TOut>> projection,
        CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await sort.Apply(query, list.Sort)
            .Skip((list.SafePage - 1) * list.SafePageSize)
            .Take(list.SafePageSize)
            .Select(projection)
            .ToListAsync(ct);
        return new PagedResult<TOut>(items, total, list.SafePage, list.SafePageSize);
    }

    /// <summary>Pattern for a case-insensitive "contains" search with Postgres ILIKE (wildcards escaped).</summary>
    public static string Like(string search) => "%" + search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}
