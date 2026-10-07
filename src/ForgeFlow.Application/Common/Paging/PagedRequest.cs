using System.ComponentModel.DataAnnotations;

namespace ForgeFlow.Application.Common.Paging;

public class PagedRequest
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    [StringLength(100)]
    public string? Search { get; set; }

    [StringLength(40)]
    public string? SortBy { get; set; }

    [RegularExpression("^(?i)(asc|desc)$", ErrorMessage = "SortDirection must be 'asc' or 'desc'.")]
    public string? SortDirection { get; set; }

    public bool IsDescending() => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
