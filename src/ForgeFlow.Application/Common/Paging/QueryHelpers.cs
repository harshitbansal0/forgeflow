using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Common.Paging;

/// <summary>Whitelist of sortable columns, so client-supplied sort keys never reach the query as raw expressions.</summary>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _sorts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _defaultKey;
    private readonly bool _defaultDescending;

    public SortMap(string defaultKey, bool defaultDescending = false)
    {
        _defaultKey = defaultKey;
        _defaultDescending = defaultDescending;
    }

    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        _sorts[key] = (query, descending) => descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
        return this;
    }

    public IOrderedQueryable<T> Apply(IQueryable<T> query, PagedRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy) && _sorts.TryGetValue(request.SortBy, out var sort))
        {
            return sort(query, request.IsDescending());
        }

        return _sorts[_defaultKey](query, string.IsNullOrWhiteSpace(request.SortDirection) ? _defaultDescending : request.IsDescending());
    }
}

public static class SearchPattern
{
    public const string EscapeCharacter = "\\";

    /// <summary>Builds a LIKE pattern for a "contains" search, escaping wildcards in the user's input.</summary>
    public static string? Contains(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var escaped = term.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");
        return $"%{escaped}%";
    }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<T>(items, request.Page, request.PageSize, total);
    }
}
