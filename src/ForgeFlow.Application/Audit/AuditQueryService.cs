using System.Text.Json;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Audit;

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogDto>> GetProductHistoryAsync(int productId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogDto>> GetComponentHistoryAsync(int componentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogDto>> GetChangeHistoryAsync(int changeId, CancellationToken cancellationToken);
}

public sealed class AuditQueryService(IAppDbContext db) : IAuditQueryService
{
    private const int HistoryLimit = 200;

    private static readonly SortMap<AuditLog> Sorts = new SortMap<AuditLog>("timestamp", defaultDescending: true)
        .Add("timestamp", a => a.TimestampUtc)
        .Add("userName", a => a.UserName)
        .Add("action", a => a.Action)
        .Add("entityType", a => a.EntityType);

    public async Task<PagedResult<AuditLogDto>> ListAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (SearchPattern.Contains(query.Search) is { } pattern)
        {
            logs = logs.Where(a => EF.Functions.Like(a.Summary!, pattern, SearchPattern.EscapeCharacter)
                                   || EF.Functions.Like(a.UserName, pattern, SearchPattern.EscapeCharacter)
                                   || a.EntityId == query.Search!.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            logs = logs.Where(a => a.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(a => a.Action == query.Action);
        }

        if (query.UserId is { } userId)
        {
            logs = logs.Where(a => a.UserId == userId);
        }

        if (query.FromUtc is { } from)
        {
            logs = logs.Where(a => a.TimestampUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            logs = logs.Where(a => a.TimestampUtc < to);
        }

        var page = await Sorts.Apply(logs, query).ThenByDescending(a => a.Id).ToPagedResultAsync(query, cancellationToken);
        return new PagedResult<AuditLogDto>(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetProductHistoryAsync(int productId, CancellationToken cancellationToken)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            throw NotFoundException.For("Product", productId);
        }

        var id = productId.ToString();
        var revisionIds = (await db.ProductRevisions
                .Where(r => r.ProductId == productId)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken))
            .Select(r => r.ToString())
            .ToList();

        return await HistoryAsync(
            db.AuditLogs.Where(a => (a.EntityType == nameof(Product) && a.EntityId == id)
                                    || (a.ParentEntityType == nameof(Product) && a.ParentEntityId == id)
                                    || (a.ParentEntityType == nameof(ProductRevision) && revisionIds.Contains(a.ParentEntityId!))),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetComponentHistoryAsync(int componentId, CancellationToken cancellationToken)
    {
        if (!await db.Components.AnyAsync(c => c.Id == componentId, cancellationToken))
        {
            throw NotFoundException.For("Component", componentId);
        }

        var id = componentId.ToString();
        return await HistoryAsync(
            db.AuditLogs.Where(a => (a.EntityType == nameof(Component) && a.EntityId == id)
                                    || (a.ParentEntityType == nameof(Component) && a.ParentEntityId == id)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetChangeHistoryAsync(int changeId, CancellationToken cancellationToken)
    {
        if (!await db.EngineeringChanges.AnyAsync(c => c.Id == changeId, cancellationToken))
        {
            throw NotFoundException.For("Engineering change", EngineeringChange.FormatNumber(changeId));
        }

        var id = changeId.ToString();
        return await HistoryAsync(
            db.AuditLogs.Where(a => (a.EntityType == nameof(EngineeringChange) && a.EntityId == id)
                                    || (a.ParentEntityType == nameof(EngineeringChange) && a.ParentEntityId == id)),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<AuditLogDto>> HistoryAsync(IQueryable<AuditLog> logs, CancellationToken cancellationToken)
    {
        var entries = await logs.AsNoTracking()
            .OrderByDescending(a => a.TimestampUtc)
            .ThenByDescending(a => a.Id)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);
        return entries.Select(ToDto).ToList();
    }

    private static AuditLogDto ToDto(AuditLog log) => new(
        log.Id,
        log.TimestampUtc,
        log.UserId,
        log.UserName,
        log.Action,
        log.EntityType,
        log.EntityId,
        log.ParentEntityType,
        log.ParentEntityId,
        log.Summary,
        string.IsNullOrEmpty(log.ChangesJson)
            ? []
            : JsonSerializer.Deserialize<List<AuditPropertyChange>>(log.ChangesJson, AuditPropertyChange.JsonOptions) ?? []);
}
