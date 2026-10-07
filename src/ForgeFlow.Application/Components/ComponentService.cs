using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Revisions;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Components;

public interface IComponentService
{
    Task<PagedResult<ComponentSummaryDto>> ListAsync(ComponentQuery query, CancellationToken cancellationToken);
    Task<ComponentDetailDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<ComponentDetailDto> CreateAsync(CreateComponentRequest request, CancellationToken cancellationToken);
    Task<ComponentDetailDto> UpdateAsync(int id, UpdateComponentRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<ComponentDetailDto> ReviseAsync(int id, CancellationToken cancellationToken);
    Task<ComponentDetailDto> UpdateRevisionAsync(int id, int revisionId, UpdateComponentRevisionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<WhereUsedDto>> GetWhereUsedAsync(int id, CancellationToken cancellationToken);
}

public sealed class ComponentService(IAppDbContext db) : IComponentService
{
    private static readonly SortMap<Component> Sorts = new SortMap<Component>("updatedAt", defaultDescending: true)
        .Add("partNumber", c => c.PartNumber)
        .Add("name", c => c.Name)
        .Add("type", c => c.Type)
        .Add("supplier", c => c.Supplier)
        .Add("lifecycleState", c => c.LifecycleState)
        .Add("updatedAt", c => c.UpdatedAtUtc ?? c.CreatedAtUtc);

    public async Task<PagedResult<ComponentSummaryDto>> ListAsync(ComponentQuery query, CancellationToken cancellationToken)
    {
        var components = db.Components.AsNoTracking();
        if (SearchPattern.Contains(query.Search) is { } pattern)
        {
            components = components.Where(c => EF.Functions.Like(c.PartNumber, pattern, SearchPattern.EscapeCharacter)
                                               || EF.Functions.Like(c.Name, pattern, SearchPattern.EscapeCharacter)
                                               || EF.Functions.Like(c.Material!, pattern, SearchPattern.EscapeCharacter)
                                               || EF.Functions.Like(c.Supplier!, pattern, SearchPattern.EscapeCharacter));
        }

        if (query.Type is { } type)
        {
            components = components.Where(c => c.Type == type);
        }

        if (query.LifecycleState is { } state)
        {
            components = components.Where(c => c.LifecycleState == state);
        }

        return await Sorts.Apply(components, query)
            .Select(c => new ComponentSummaryDto(
                c.Id,
                c.PartNumber,
                c.Name,
                c.Type,
                c.Material,
                c.UnitOfMeasure,
                c.Supplier,
                c.UnitCost,
                c.LifecycleState,
                c.Revisions.Where(r => r.Status == RevisionStatus.Released).Select(r => r.RevisionCode).FirstOrDefault(),
                c.Revisions.Where(r => r.Status == RevisionStatus.Draft || r.Status == RevisionStatus.InReview)
                    .Select(r => r.RevisionCode).FirstOrDefault(),
                c.Revisions.Where(r => r.Status == RevisionStatus.Draft || r.Status == RevisionStatus.InReview)
                    .Select(r => (RevisionStatus?)r.Status).FirstOrDefault(),
                c.UpdatedAtUtc ?? c.CreatedAtUtc))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ComponentDetailDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var component = await db.Components.AsNoTracking()
                            .Include(c => c.Revisions)
                            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                        ?? throw NotFoundException.For("Component", id);

        var openChanges = (await db.ChangeAffectedItems.AsNoTracking()
                .WhereChangeIsOpen()
                .Where(a => a.ComponentRevisionId != null && a.ComponentRevision!.ComponentId == id)
                .Select(a => new { RevisionId = a.ComponentRevisionId!.Value, a.EngineeringChangeId })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.RevisionId)
            .ToDictionary(g => g.Key, g => g.First().EngineeringChangeId);

        return new ComponentDetailDto(
            component.Id,
            component.PartNumber,
            component.Name,
            component.Description,
            component.Type,
            component.Material,
            component.UnitOfMeasure,
            component.Supplier,
            component.UnitCost,
            component.LifecycleState,
            component.CreatedAtUtc,
            component.CreatedBy,
            component.UpdatedAtUtc,
            component.UpdatedBy,
            component.Revisions
                .OrderByRevision()
                .Reverse()
                .Select(r => RevisionDto.From(r, openChanges.TryGetValue(r.Id, out var changeId) ? changeId : null))
                .ToList());
    }

    public async Task<ComponentDetailDto> CreateAsync(CreateComponentRequest request, CancellationToken cancellationToken)
    {
        var number = ItemNumbers.Normalize(request.PartNumber);
        if (await db.Components.AnyAsync(c => c.PartNumber == number, cancellationToken))
        {
            throw new ConflictException($"Part number {number} is already in use.");
        }

        var component = new Component { PartNumber = number };
        Apply(component, request);
        var revision = component.Revise("Initial revision");
        revision.DrawingNumber = request.DrawingNumber.TrimToNull();
        revision.WeightKg = request.WeightKg;

        db.Components.Add(component);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(component.Id, cancellationToken);
    }

    public async Task<ComponentDetailDto> UpdateAsync(int id, UpdateComponentRequest request, CancellationToken cancellationToken)
    {
        var component = await db.Components.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                        ?? throw NotFoundException.For("Component", id);
        Apply(component, request);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var component = await db.Components
                            .Include(c => c.Revisions)
                            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                        ?? throw NotFoundException.For("Component", id);

        if (component.Revisions.Any(r => r.Status is RevisionStatus.Released or RevisionStatus.Superseded))
        {
            throw new ConflictException($"{component.PartNumber} has released revisions and must be kept for traceability. Mark it Obsolete instead.");
        }

        if (await db.BomItems.AnyAsync(i => i.ComponentId == id, cancellationToken))
        {
            throw new ConflictException($"{component.PartNumber} is used in a product BOM.");
        }

        if (await db.ChangeAffectedItems.AnyAsync(a => a.ComponentRevision != null && a.ComponentRevision.ComponentId == id, cancellationToken))
        {
            throw new ConflictException($"{component.PartNumber} is referenced by an engineering change.");
        }

        db.Components.Remove(component);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ComponentDetailDto> ReviseAsync(int id, CancellationToken cancellationToken)
    {
        var component = await db.Components
                            .Include(c => c.Revisions)
                            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                        ?? throw NotFoundException.For("Component", id);

        component.Revise();
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ComponentDetailDto> UpdateRevisionAsync(
        int id, int revisionId, UpdateComponentRevisionRequest request, CancellationToken cancellationToken)
    {
        var revision = await db.ComponentRevisions.FirstOrDefaultAsync(r => r.Id == revisionId && r.ComponentId == id, cancellationToken)
                       ?? throw NotFoundException.For("Component revision", revisionId);

        revision.EnsureEditable();
        revision.ChangeSummary = request.ChangeSummary.TrimToNull();
        revision.DrawingNumber = request.DrawingNumber.TrimToNull();
        revision.WeightKg = request.WeightKg;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<WhereUsedDto>> GetWhereUsedAsync(int id, CancellationToken cancellationToken)
    {
        if (!await db.Components.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw NotFoundException.For("Component", id);
        }

        return await db.BomItems.AsNoTracking()
            .Where(i => i.ComponentId == id)
            .OrderBy(i => i.ProductRevision.Product.ProductNumber)
            .ThenBy(i => i.ProductRevision.RevisionCode.Length)
            .ThenBy(i => i.ProductRevision.RevisionCode)
            .Select(i => new WhereUsedDto(
                i.ProductRevision.ProductId,
                i.ProductRevision.Product.ProductNumber,
                i.ProductRevision.Product.Name,
                i.ProductRevisionId,
                i.ProductRevision.RevisionCode,
                i.ProductRevision.Status,
                i.Quantity,
                i.ReferenceDesignator))
            .ToListAsync(cancellationToken);
    }

    private static void Apply(Component component, ComponentFields fields)
    {
        component.Name = fields.Name.Trim();
        component.Description = fields.Description.TrimToNull();
        component.Type = fields.Type;
        component.Material = fields.Material.TrimToNull();
        component.UnitOfMeasure = fields.UnitOfMeasure.Trim().ToUpperInvariant();
        component.Supplier = fields.Supplier.TrimToNull();
        component.UnitCost = fields.UnitCost;
        component.LifecycleState = fields.LifecycleState;
    }
}
