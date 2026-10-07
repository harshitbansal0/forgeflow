using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductSummaryDto>> ListAsync(ProductQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<ProductDetailDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<ProductRevisionDetailDto> GetRevisionAsync(int productId, int revisionId, CancellationToken cancellationToken);
    Task<ProductRevisionDetailDto> ReviseAsync(int productId, CancellationToken cancellationToken);
    Task<ProductRevisionDetailDto> UpdateRevisionAsync(int productId, int revisionId, UpdateRevisionRequest request, CancellationToken cancellationToken);
    Task<ProductRevisionDetailDto> AddBomItemAsync(int productId, int revisionId, AddBomItemRequest request, CancellationToken cancellationToken);
    Task<ProductRevisionDetailDto> UpdateBomItemAsync(int productId, int revisionId, int bomItemId, UpdateBomItemRequest request, CancellationToken cancellationToken);
    Task<ProductRevisionDetailDto> RemoveBomItemAsync(int productId, int revisionId, int bomItemId, CancellationToken cancellationToken);
    Task<RevisionComparisonDto> CompareRevisionsAsync(int productId, int fromRevisionId, int toRevisionId, CancellationToken cancellationToken);
}

public sealed class ProductService(IAppDbContext db, ICurrentUser currentUser) : IProductService
{
    private static readonly SortMap<Product> Sorts = new SortMap<Product>("updatedAt", defaultDescending: true)
        .Add("productNumber", p => p.ProductNumber)
        .Add("name", p => p.Name)
        .Add("category", p => p.Category)
        .Add("lifecycleState", p => p.LifecycleState)
        .Add("owner", p => p.Owner.DisplayName)
        .Add("updatedAt", p => p.UpdatedAtUtc ?? p.CreatedAtUtc);

    public async Task<PagedResult<ProductSummaryDto>> ListAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking();
        if (SearchPattern.Contains(query.Search) is { } pattern)
        {
            products = products.Where(p => EF.Functions.Like(p.ProductNumber, pattern, SearchPattern.EscapeCharacter)
                                           || EF.Functions.Like(p.Name, pattern, SearchPattern.EscapeCharacter)
                                           || EF.Functions.Like(p.Category, pattern, SearchPattern.EscapeCharacter));
        }

        if (query.LifecycleState is { } state)
        {
            products = products.Where(p => p.LifecycleState == state);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            products = products.Where(p => p.Category == query.Category);
        }

        return await Sorts.Apply(products, query)
            .Select(p => new ProductSummaryDto(
                p.Id,
                p.ProductNumber,
                p.Name,
                p.Category,
                p.LifecycleState,
                p.Owner.DisplayName,
                p.Revisions.Where(r => r.Status == RevisionStatus.Released).Select(r => r.RevisionCode).FirstOrDefault(),
                p.Revisions.Where(r => r.Status == RevisionStatus.Draft || r.Status == RevisionStatus.InReview)
                    .Select(r => r.RevisionCode).FirstOrDefault(),
                p.Revisions.Where(r => r.Status == RevisionStatus.Draft || r.Status == RevisionStatus.InReview)
                    .Select(r => (RevisionStatus?)r.Status).FirstOrDefault(),
                p.UpdatedAtUtc ?? p.CreatedAtUtc))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

    public async Task<ProductDetailDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
                          .Include(p => p.Owner)
                          .Include(p => p.Revisions)
                          .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                      ?? throw NotFoundException.For("Product", id);

        var openChanges = (await db.ChangeAffectedItems.AsNoTracking()
                .WhereChangeIsOpen()
                .Where(a => a.ProductRevisionId != null && a.ProductRevision!.ProductId == id)
                .Select(a => new { RevisionId = a.ProductRevisionId!.Value, a.EngineeringChangeId })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.RevisionId)
            .ToDictionary(g => g.Key, g => g.First().EngineeringChangeId);

        return new ProductDetailDto(
            product.Id,
            product.ProductNumber,
            product.Name,
            product.Description,
            product.Category,
            product.LifecycleState,
            product.OwnerId,
            product.Owner.DisplayName,
            product.CreatedAtUtc,
            product.CreatedBy,
            product.UpdatedAtUtc,
            product.UpdatedBy,
            product.Revisions
                .OrderByRevision()
                .Reverse()
                .Select(r => RevisionDto.From(r, openChanges.TryGetValue(r.Id, out var changeId) ? changeId : null))
                .ToList());
    }

    public async Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var actingUser = await db.GetActingUserAsync(currentUser, cancellationToken);
        var number = ItemNumbers.Normalize(request.ProductNumber);
        if (await db.Products.AnyAsync(p => p.ProductNumber == number, cancellationToken))
        {
            throw new ConflictException($"Product number {number} is already in use.");
        }

        var ownerId = request.OwnerId ?? actingUser.Id;
        await EnsureActiveOwnerAsync(ownerId, cancellationToken);

        var product = new Product
        {
            ProductNumber = number,
            Name = request.Name.Trim(),
            Description = request.Description.TrimToNull(),
            Category = request.Category.Trim(),
            LifecycleState = request.LifecycleState,
            OwnerId = ownerId
        };
        product.Revise(request.InitialChangeSummary.TrimToNull() ?? "Initial revision");

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                      ?? throw NotFoundException.For("Product", id);
        await EnsureActiveOwnerAsync(request.OwnerId, cancellationToken);

        product.Name = request.Name.Trim();
        product.Description = request.Description.TrimToNull();
        product.Category = request.Category.Trim();
        product.LifecycleState = request.LifecycleState;
        product.OwnerId = request.OwnerId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products
                          .Include(p => p.Revisions).ThenInclude(r => r.BomItems)
                          .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                      ?? throw NotFoundException.For("Product", id);

        if (product.Revisions.Any(r => r.Status is RevisionStatus.Released or RevisionStatus.Superseded))
        {
            throw new ConflictException($"{product.ProductNumber} has released revisions and must be kept for traceability. Mark it Obsolete instead.");
        }

        if (await db.ChangeAffectedItems.AnyAsync(a => a.ProductRevision != null && a.ProductRevision.ProductId == id, cancellationToken))
        {
            throw new ConflictException($"{product.ProductNumber} is referenced by an engineering change.");
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductRevisionDetailDto> GetRevisionAsync(int productId, int revisionId, CancellationToken cancellationToken)
    {
        var revision = await db.ProductRevisions.AsNoTracking()
                           .Include(r => r.Product)
                           .Include(r => r.BomItems).ThenInclude(i => i.Component).ThenInclude(c => c.Revisions)
                           .FirstOrDefaultAsync(r => r.Id == revisionId && r.ProductId == productId, cancellationToken)
                       ?? throw NotFoundException.For("Product revision", revisionId);

        var openChangeId = await db.ChangeAffectedItems.AsNoTracking()
            .WhereChangeIsOpen()
            .Where(a => a.ProductRevisionId == revisionId)
            .Select(a => (int?)a.EngineeringChangeId)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProductRevisionDetailDto(
            revision.ProductId,
            revision.Product.ProductNumber,
            revision.Product.Name,
            RevisionDto.From(revision, openChangeId),
            revision.BomItems
                .OrderBy(i => i.Component.PartNumber, StringComparer.Ordinal)
                .Select(i => new BomItemDto(
                    i.Id,
                    i.ComponentId,
                    i.Component.PartNumber,
                    i.Component.Name,
                    i.Component.Type,
                    i.Component.UnitOfMeasure,
                    i.Quantity,
                    i.ReferenceDesignator,
                    i.Notes,
                    i.Component.GetReleasedRevision()?.RevisionCode,
                    i.Component.LifecycleState))
                .ToList());
    }

    public async Task<ProductRevisionDetailDto> ReviseAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await db.Products
                          .Include(p => p.Revisions).ThenInclude(r => r.BomItems)
                          .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
                      ?? throw NotFoundException.For("Product", productId);

        var revision = product.Revise();
        await db.SaveChangesAsync(cancellationToken);
        return await GetRevisionAsync(productId, revision.Id, cancellationToken);
    }

    public async Task<ProductRevisionDetailDto> UpdateRevisionAsync(
        int productId, int revisionId, UpdateRevisionRequest request, CancellationToken cancellationToken)
    {
        var revision = await LoadRevisionForEditAsync(productId, revisionId, cancellationToken);
        revision.EnsureEditable();
        revision.ChangeSummary = request.ChangeSummary.TrimToNull();
        await db.SaveChangesAsync(cancellationToken);
        return await GetRevisionAsync(productId, revisionId, cancellationToken);
    }

    public async Task<ProductRevisionDetailDto> AddBomItemAsync(
        int productId, int revisionId, AddBomItemRequest request, CancellationToken cancellationToken)
    {
        var revision = await LoadRevisionForEditAsync(productId, revisionId, cancellationToken);
        var component = await db.Components.FirstOrDefaultAsync(c => c.Id == request.ComponentId, cancellationToken)
                        ?? throw new RequestValidationException(nameof(request.ComponentId), $"Component {request.ComponentId} was not found.");

        revision.AddBomItem(component, request.Quantity, request.ReferenceDesignator.TrimToNull(), request.Notes.TrimToNull());
        await db.SaveChangesAsync(cancellationToken);
        return await GetRevisionAsync(productId, revisionId, cancellationToken);
    }

    public async Task<ProductRevisionDetailDto> UpdateBomItemAsync(
        int productId, int revisionId, int bomItemId, UpdateBomItemRequest request, CancellationToken cancellationToken)
    {
        var revision = await LoadRevisionForEditAsync(productId, revisionId, cancellationToken);
        var item = revision.BomItems.FirstOrDefault(i => i.Id == bomItemId) ?? throw NotFoundException.For("BOM item", bomItemId);

        revision.UpdateBomItem(item, request.Quantity, request.ReferenceDesignator.TrimToNull(), request.Notes.TrimToNull());
        await db.SaveChangesAsync(cancellationToken);
        return await GetRevisionAsync(productId, revisionId, cancellationToken);
    }

    public async Task<ProductRevisionDetailDto> RemoveBomItemAsync(
        int productId, int revisionId, int bomItemId, CancellationToken cancellationToken)
    {
        var revision = await LoadRevisionForEditAsync(productId, revisionId, cancellationToken);
        var item = revision.BomItems.FirstOrDefault(i => i.Id == bomItemId) ?? throw NotFoundException.For("BOM item", bomItemId);

        revision.RemoveBomItem(item);
        await db.SaveChangesAsync(cancellationToken);
        return await GetRevisionAsync(productId, revisionId, cancellationToken);
    }

    public async Task<RevisionComparisonDto> CompareRevisionsAsync(
        int productId, int fromRevisionId, int toRevisionId, CancellationToken cancellationToken)
    {
        var revisions = await db.ProductRevisions.AsNoTracking()
            .Include(r => r.BomItems).ThenInclude(i => i.Component)
            .Where(r => r.ProductId == productId && (r.Id == fromRevisionId || r.Id == toRevisionId))
            .ToListAsync(cancellationToken);

        var from = revisions.FirstOrDefault(r => r.Id == fromRevisionId) ?? throw NotFoundException.For("Product revision", fromRevisionId);
        var to = revisions.FirstOrDefault(r => r.Id == toRevisionId) ?? throw NotFoundException.For("Product revision", toRevisionId);

        var components = from.BomItems.Concat(to.BomItems)
            .Select(i => i.Component)
            .DistinctBy(c => c.Id)
            .ToDictionary(c => c.Id);

        var differences = BomComparer
            .Compare(
                from.BomItems.Select(i => new BomLine(i.ComponentId, i.Quantity)),
                to.BomItems.Select(i => new BomLine(i.ComponentId, i.Quantity)))
            .Select(d => new BomDifferenceDto(
                d.ComponentId,
                components[d.ComponentId].PartNumber,
                components[d.ComponentId].Name,
                d.Kind,
                d.FromQuantity,
                d.ToQuantity))
            .OrderBy(d => d.PartNumber, StringComparer.Ordinal)
            .ToList();

        return new RevisionComparisonDto(from.Id, from.RevisionCode, to.Id, to.RevisionCode, differences);
    }

    private async Task<ProductRevision> LoadRevisionForEditAsync(int productId, int revisionId, CancellationToken cancellationToken) =>
        await db.ProductRevisions
            .Include(r => r.BomItems)
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.ProductId == productId, cancellationToken)
        ?? throw NotFoundException.For("Product revision", revisionId);

    private async Task EnsureActiveOwnerAsync(int ownerId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == ownerId && u.IsActive, cancellationToken))
        {
            throw new RequestValidationException("ownerId", "The owner must be an active user.");
        }
    }
}
