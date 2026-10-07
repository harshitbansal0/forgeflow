using ForgeFlow.Application.Approvals;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Dashboard;

public sealed record CountByKey(string Key, int Count);

public sealed record RecentReleaseDto(
    string ItemType,
    int ItemId,
    string ItemNumber,
    string ItemName,
    string RevisionCode,
    DateTime ReleasedAtUtc,
    string? ReleasedBy);

public sealed record ActivityDto(
    long Id,
    DateTime TimestampUtc,
    string UserName,
    string Action,
    string EntityType,
    string EntityId,
    string? Summary);

public sealed record DashboardDto(
    int ProductCount,
    int ComponentCount,
    int OpenChangeCount,
    int PendingApprovalCount,
    IReadOnlyList<CountByKey> ProductsByLifecycle,
    IReadOnlyList<CountByKey> ChangesByStatus,
    IReadOnlyList<RecentReleaseDto> RecentReleases,
    IReadOnlyList<ActivityDto> RecentActivity);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
}

public sealed class DashboardService(IAppDbContext db, IApprovalService approvals) : IDashboardService
{
    private const int RecentCount = 6;

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var productCount = await db.Products.CountAsync(cancellationToken);
        var componentCount = await db.Components.CountAsync(cancellationToken);
        var openChangeCount = await db.EngineeringChanges.CountAsync(
            c => c.Status == ChangeStatus.Draft || c.Status == ChangeStatus.InReview || c.Status == ChangeStatus.Approved,
            cancellationToken);
        var pendingApprovals = await approvals.CountPendingAsync(cancellationToken);

        var lifecycleCounts = await db.Products
            .GroupBy(p => p.LifecycleState)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var statusCounts = await db.EngineeringChanges
            .GroupBy(c => c.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var productReleases = await db.ProductRevisions.AsNoTracking()
            .Where(r => r.ReleasedAtUtc != null)
            .OrderByDescending(r => r.ReleasedAtUtc)
            .Take(RecentCount)
            .Select(r => new RecentReleaseDto("Product", r.ProductId, r.Product.ProductNumber, r.Product.Name,
                r.RevisionCode, r.ReleasedAtUtc!.Value, r.ReleasedBy))
            .ToListAsync(cancellationToken);
        var componentReleases = await db.ComponentRevisions.AsNoTracking()
            .Where(r => r.ReleasedAtUtc != null)
            .OrderByDescending(r => r.ReleasedAtUtc)
            .Take(RecentCount)
            .Select(r => new RecentReleaseDto("Component", r.ComponentId, r.Component.PartNumber, r.Component.Name,
                r.RevisionCode, r.ReleasedAtUtc!.Value, r.ReleasedBy))
            .ToListAsync(cancellationToken);

        var recentActivity = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Action != AuditActions.LoginSucceeded && a.Action != AuditActions.LoginFailed)
            .OrderByDescending(a => a.TimestampUtc)
            .ThenByDescending(a => a.Id)
            .Take(10)
            .Select(a => new ActivityDto(a.Id, a.TimestampUtc, a.UserName, a.Action, a.EntityType, a.EntityId, a.Summary))
            .ToListAsync(cancellationToken);

        return new DashboardDto(
            productCount,
            componentCount,
            openChangeCount,
            pendingApprovals,
            Enum.GetValues<LifecycleState>()
                .Select(s => new CountByKey(s.ToString(), lifecycleCounts.GetValueOrDefault(s)))
                .ToList(),
            Enum.GetValues<ChangeStatus>()
                .Select(s => new CountByKey(s.ToString(), statusCounts.GetValueOrDefault(s)))
                .ToList(),
            productReleases.Concat(componentReleases)
                .OrderByDescending(r => r.ReleasedAtUtc)
                .Take(RecentCount)
                .ToList(),
            recentActivity);
    }
}
