using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Changes;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Search;

public sealed record SearchResultDto(string Type, int Id, string Number, string Title, string Status);

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(string? term, CancellationToken cancellationToken);
}

public sealed class SearchService(IAppDbContext db) : ISearchService
{
    private const int MaxPerType = 5;

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(string? term, CancellationToken cancellationToken)
    {
        if (term is null || term.Trim().Length < 2 || SearchPattern.Contains(term) is not { } pattern)
        {
            return [];
        }

        var products = await db.Products.AsNoTracking()
            .Where(p => EF.Functions.Like(p.ProductNumber, pattern, SearchPattern.EscapeCharacter)
                        || EF.Functions.Like(p.Name, pattern, SearchPattern.EscapeCharacter))
            .OrderBy(p => p.ProductNumber)
            .Take(MaxPerType)
            .Select(p => new { p.Id, p.ProductNumber, p.Name, p.LifecycleState })
            .ToListAsync(cancellationToken);

        var components = await db.Components.AsNoTracking()
            .Where(c => EF.Functions.Like(c.PartNumber, pattern, SearchPattern.EscapeCharacter)
                        || EF.Functions.Like(c.Name, pattern, SearchPattern.EscapeCharacter))
            .OrderBy(c => c.PartNumber)
            .Take(MaxPerType)
            .Select(c => new { c.Id, c.PartNumber, c.Name, c.LifecycleState })
            .ToListAsync(cancellationToken);

        var changeQuery = db.EngineeringChanges.AsNoTracking();
        changeQuery = ChangeNumbers.TryParse(term, out var changeId)
            ? changeQuery.Where(c => c.Id == changeId)
            : changeQuery.Where(c => EF.Functions.Like(c.Title, pattern, SearchPattern.EscapeCharacter));
        var changes = await changeQuery
            .OrderByDescending(c => c.Id)
            .Take(MaxPerType)
            .Select(c => new { c.Id, c.Title, c.Status })
            .ToListAsync(cancellationToken);

        return products.Select(p => new SearchResultDto("Product", p.Id, p.ProductNumber, p.Name, p.LifecycleState.ToString()))
            .Concat(components.Select(c => new SearchResultDto("Component", c.Id, c.PartNumber, c.Name, c.LifecycleState.ToString())))
            .Concat(changes.Select(c => new SearchResultDto("EngineeringChange", c.Id, EngineeringChange.FormatNumber(c.Id), c.Title, c.Status.ToString())))
            .ToList();
    }
}
