using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Changes;

public interface IEngineeringChangeService
{
    Task<PagedResult<ChangeSummaryDto>> ListAsync(ChangeQuery query, CancellationToken cancellationToken);
    Task<ChangeDetailDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<ChangeDetailDto> CreateAsync(CreateChangeRequest request, CancellationToken cancellationToken);
    Task<ChangeDetailDto> UpdateAsync(int id, UpdateChangeRequest request, CancellationToken cancellationToken);
    Task<ChangeDetailDto> AddAffectedItemAsync(int id, AffectedItemRequest request, CancellationToken cancellationToken);
    Task<ChangeDetailDto> RemoveAffectedItemAsync(int id, int affectedItemId, CancellationToken cancellationToken);
    Task<ChangeDetailDto> SubmitAsync(int id, CancellationToken cancellationToken);
    Task<ChangeDetailDto> ApproveAsync(int id, DecisionRequest request, CancellationToken cancellationToken);
    Task<ChangeDetailDto> RejectAsync(int id, DecisionRequest request, CancellationToken cancellationToken);
    Task<ChangeDetailDto> ImplementAsync(int id, CancellationToken cancellationToken);
    Task<ChangeDetailDto> CancelAsync(int id, DecisionRequest request, CancellationToken cancellationToken);
}

public sealed class EngineeringChangeService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    TimeProvider clock) : IEngineeringChangeService
{
    private const string EntityType = nameof(EngineeringChange);

    private static readonly SortMap<EngineeringChange> Sorts = new SortMap<EngineeringChange>("updatedAt", defaultDescending: true)
        .Add("changeNumber", c => c.Id)
        .Add("title", c => c.Title)
        .Add("status", c => c.Status)
        .Add("priority", c => c.Priority == ChangePriority.Critical ? 3
            : c.Priority == ChangePriority.High ? 2
            : c.Priority == ChangePriority.Medium ? 1
            : 0)
        .Add("requestedBy", c => c.RequestedBy.DisplayName)
        .Add("createdAt", c => c.CreatedAtUtc)
        .Add("submittedAt", c => c.SubmittedAtUtc)
        .Add("updatedAt", c => c.LastActivityAtUtc ?? c.UpdatedAtUtc ?? c.CreatedAtUtc);

    public async Task<PagedResult<ChangeSummaryDto>> ListAsync(ChangeQuery query, CancellationToken cancellationToken)
    {
        var changes = db.EngineeringChanges.AsNoTracking();
        if (ChangeNumbers.TryParse(query.Search, out var changeId))
        {
            changes = changes.Where(c => c.Id == changeId);
        }
        else if (SearchPattern.Contains(query.Search) is { } pattern)
        {
            changes = changes.Where(c => EF.Functions.Like(c.Title, pattern, SearchPattern.EscapeCharacter)
                                         || EF.Functions.Like(c.Description, pattern, SearchPattern.EscapeCharacter));
        }

        if (query.Status is { } status)
        {
            changes = changes.Where(c => c.Status == status);
        }

        if (query.Priority is { } priority)
        {
            changes = changes.Where(c => c.Priority == priority);
        }

        if (query.RequestedByMe == true && currentUser.UserId is { } userId)
        {
            changes = changes.Where(c => c.RequestedById == userId);
        }

        var page = await Sorts.Apply(changes, query)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Status,
                c.Priority,
                RequestedBy = c.RequestedBy.DisplayName,
                WorkflowName = c.WorkflowDefinition.Name,
                AffectedItemCount = c.AffectedItems.Count,
                CurrentStepName = c.ApprovalSteps
                    .Where(s => s.Status == ApprovalStepStatus.Active)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                c.CreatedAtUtc,
                c.SubmittedAtUtc,
                UpdatedAtUtc = c.LastActivityAtUtc ?? c.UpdatedAtUtc ?? c.CreatedAtUtc
            })
            .ToPagedResultAsync(query, cancellationToken);

        var items = page.Items
            .Select(c => new ChangeSummaryDto(
                c.Id,
                EngineeringChange.FormatNumber(c.Id),
                c.Title,
                c.Status,
                c.Priority,
                c.RequestedBy,
                c.WorkflowName,
                c.AffectedItemCount,
                c.CurrentStepName,
                c.CreatedAtUtc,
                c.SubmittedAtUtc,
                c.UpdatedAtUtc))
            .ToList();
        return new PagedResult<ChangeSummaryDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<ChangeDetailDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var change = await LoadAsync(id, cancellationToken);
        var user = await db.GetActingUserAsync(currentUser, cancellationToken);
        return ToDetail(change, user);
    }

    public async Task<ChangeDetailDto> CreateAsync(CreateChangeRequest request, CancellationToken cancellationToken)
    {
        var user = await db.GetActingUserAsync(currentUser, cancellationToken);
        var workflow = await ResolveWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);

        var change = new EngineeringChange
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Reason = request.Reason.TrimToNull(),
            Priority = request.Priority,
            RequestedById = user.Id,
            WorkflowDefinitionId = workflow.Id
        };

        foreach (var item in request.AffectedItems)
        {
            await AddAffectedRevisionAsync(change, item, cancellationToken);
        }

        db.EngineeringChanges.Add(change);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(change.Id, cancellationToken);
    }

    public async Task<ChangeDetailDto> UpdateAsync(int id, UpdateChangeRequest request, CancellationToken cancellationToken)
    {
        var (change, user) = await LoadForManagementAsync(id, cancellationToken);
        change.EnsureEditable();
        var workflow = await ResolveWorkflowAsync(request.WorkflowDefinitionId, cancellationToken);

        change.Title = request.Title.Trim();
        change.Description = request.Description.Trim();
        change.Reason = request.Reason.TrimToNull();
        change.Priority = request.Priority;
        change.WorkflowDefinition = workflow;
        change.WorkflowDefinitionId = workflow.Id;
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    public async Task<ChangeDetailDto> AddAffectedItemAsync(int id, AffectedItemRequest request, CancellationToken cancellationToken)
    {
        var (change, user) = await LoadForManagementAsync(id, cancellationToken);
        change.EnsureEditable();
        await AddAffectedRevisionAsync(change, request, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    public async Task<ChangeDetailDto> RemoveAffectedItemAsync(int id, int affectedItemId, CancellationToken cancellationToken)
    {
        var (change, user) = await LoadForManagementAsync(id, cancellationToken);
        var item = change.AffectedItems.FirstOrDefault(a => a.Id == affectedItemId)
                   ?? throw NotFoundException.For("Affected item", affectedItemId);

        change.RemoveAffectedItem(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    public async Task<ChangeDetailDto> SubmitAsync(int id, CancellationToken cancellationToken)
    {
        var (change, user) = await LoadForManagementAsync(id, cancellationToken);
        var workflow = await db.WorkflowDefinitions
            .Include(w => w.Steps)
            .FirstAsync(w => w.Id == change.WorkflowDefinitionId, cancellationToken);
        if (!workflow.IsActive)
        {
            throw new ConflictException($"Workflow '{workflow.Name}' is inactive; choose another workflow before submitting.");
        }

        change.Submit(workflow.Steps, Now());
        auditTrail.Record(AuditActions.Submitted, EntityType, id.ToString(),
            $"{change.ChangeNumber} submitted for approval via '{workflow.Name}' ({workflow.Steps.Count} steps)");
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    public Task<ChangeDetailDto> ApproveAsync(int id, DecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(id, ApprovalOutcome.Approved, request.Comment, cancellationToken);

    public Task<ChangeDetailDto> RejectAsync(int id, DecisionRequest request, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(request.Comment)
            ? throw new RequestValidationException(nameof(request.Comment), "A comment is required when rejecting a change.")
            : DecideAsync(id, ApprovalOutcome.Rejected, request.Comment, cancellationToken);

    public async Task<ChangeDetailDto> ImplementAsync(int id, CancellationToken cancellationToken)
    {
        var (change, user) = await LoadForManagementAsync(id, cancellationToken);
        var released = change.Implement(user.DisplayName, Now());

        auditTrail.Record(AuditActions.Implemented, EntityType, id.ToString(),
            $"{change.ChangeNumber} implemented; {released.Count} revision(s) released");
        foreach (var revision in released)
        {
            var (revisionType, parentType, parentId, label) = revision switch
            {
                ProductRevision p => (nameof(ProductRevision), nameof(Product), p.ProductId, $"{p.Product.ProductNumber} Rev {p.RevisionCode}"),
                ComponentRevision c => (nameof(ComponentRevision), nameof(Component), c.ComponentId, $"{c.Component.PartNumber} Rev {c.RevisionCode}"),
                _ => throw new InvalidOperationException($"Unexpected revision type {revision.GetType().Name}.")
            };
            auditTrail.Record(AuditActions.Released, revisionType, revision.Id.ToString(),
                $"{label} released by {change.ChangeNumber}", parentType, parentId.ToString());
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    public async Task<ChangeDetailDto> CancelAsync(int id, DecisionRequest request, CancellationToken cancellationToken)
    {
        var (change, user) = await LoadForManagementAsync(id, cancellationToken);
        change.Cancel(Now());

        var reason = request.Comment.TrimToNull();
        auditTrail.Record(AuditActions.Cancelled, EntityType, id.ToString(),
            reason is null ? $"{change.ChangeNumber} cancelled by {user.DisplayName}" : $"{change.ChangeNumber} cancelled by {user.DisplayName}: {reason}");
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    private async Task<ChangeDetailDto> DecideAsync(int id, ApprovalOutcome outcome, string? comment, CancellationToken cancellationToken)
    {
        var change = await LoadAsync(id, cancellationToken);
        var user = await db.GetActingUserAsync(currentUser, cancellationToken);

        var blocker = change.GetDecisionBlocker(user.Id, user.Role);
        if (blocker is not null)
        {
            throw change.Status == ChangeStatus.InReview ? new ForbiddenException(blocker) : new ConflictException(blocker);
        }

        var stepName = change.GetActiveStep()!.Name;
        var result = change.RecordDecision(user.Id, user.Role, outcome, comment, Now());
        var number = change.ChangeNumber;
        var summary = result switch
        {
            DecisionResult.ChangeRejected => $"{number} rejected at '{stepName}' by {user.DisplayName}: {comment!.Trim()}",
            DecisionResult.ChangeApproved => $"{number} approved at '{stepName}' by {user.DisplayName}; all approval steps complete",
            DecisionResult.StepAdvanced => $"{number} approved at '{stepName}' by {user.DisplayName}; moved to '{change.GetActiveStep()!.Name}'",
            _ => $"{number} approved at '{stepName}' by {user.DisplayName}; awaiting further approvals"
        };
        auditTrail.Record(outcome == ApprovalOutcome.Approved ? AuditActions.Approved : AuditActions.Rejected, EntityType, id.ToString(), summary);

        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(change, user);
    }

    private async Task AddAffectedRevisionAsync(EngineeringChange change, AffectedItemRequest request, CancellationToken cancellationToken)
    {
        RevisionBase revision = request.ItemType switch
        {
            AffectedItemType.Product => await ResolveProductRevisionAsync(request.ItemId, change.Id, cancellationToken),
            AffectedItemType.Component => await ResolveComponentRevisionAsync(request.ItemId, change.Id, cancellationToken),
            _ => throw new RequestValidationException(nameof(request.ItemType), "Unknown item type.")
        };
        change.AddAffectedRevision(revision, request.Note.TrimToNull());
    }

    private async Task<ProductRevision> ResolveProductRevisionAsync(int productId, int changeId, CancellationToken cancellationToken)
    {
        var product = await db.Products
                          .Include(p => p.Revisions).ThenInclude(r => r.BomItems)
                          .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
                      ?? throw new RequestValidationException("itemId", $"Product {productId} was not found.");

        var revision = product.GetWorkingRevision() ?? product.Revise();
        if (revision.Id != 0 && await db.ChangeAffectedItems.WhereChangeIsOpen()
                .AnyAsync(a => a.ProductRevisionId == revision.Id && a.EngineeringChangeId != changeId, cancellationToken))
        {
            throw new ConflictException($"{product.ProductNumber} Rev {revision.RevisionCode} is already part of another open engineering change.");
        }

        return revision;
    }

    private async Task<ComponentRevision> ResolveComponentRevisionAsync(int componentId, int changeId, CancellationToken cancellationToken)
    {
        var component = await db.Components
                            .Include(c => c.Revisions)
                            .FirstOrDefaultAsync(c => c.Id == componentId, cancellationToken)
                        ?? throw new RequestValidationException("itemId", $"Component {componentId} was not found.");

        var revision = component.GetWorkingRevision() ?? component.Revise();
        if (revision.Id != 0 && await db.ChangeAffectedItems.WhereChangeIsOpen()
                .AnyAsync(a => a.ComponentRevisionId == revision.Id && a.EngineeringChangeId != changeId, cancellationToken))
        {
            throw new ConflictException($"{component.PartNumber} Rev {revision.RevisionCode} is already part of another open engineering change.");
        }

        return revision;
    }

    private async Task<WorkflowDefinition> ResolveWorkflowAsync(int? workflowId, CancellationToken cancellationToken)
    {
        var workflow = workflowId is { } id
            ? await db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            : await db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.IsDefault && w.IsActive, cancellationToken);

        if (workflow is null)
        {
            throw new RequestValidationException("workflowDefinitionId",
                workflowId is null ? "No default workflow is configured." : $"Workflow {workflowId} was not found.");
        }

        return workflow.IsActive
            ? workflow
            : throw new RequestValidationException("workflowDefinitionId", $"Workflow '{workflow.Name}' is inactive.");
    }

    private async Task<(EngineeringChange Change, User User)> LoadForManagementAsync(int id, CancellationToken cancellationToken)
    {
        var change = await LoadAsync(id, cancellationToken);
        var user = await db.GetActingUserAsync(currentUser, cancellationToken);
        if (!CanManage(change, user))
        {
            throw new ForbiddenException("Only the requester or an administrator can manage this change.");
        }

        return (change, user);
    }

    private async Task<EngineeringChange> LoadAsync(int id, CancellationToken cancellationToken) =>
        await db.EngineeringChanges
            .Include(c => c.RequestedBy)
            .Include(c => c.WorkflowDefinition)
            .Include(c => c.AffectedItems)
                .ThenInclude(a => a.ProductRevision)
                .ThenInclude(r => r!.Product)
                .ThenInclude(p => p.Revisions)
            .Include(c => c.AffectedItems)
                .ThenInclude(a => a.ComponentRevision)
                .ThenInclude(r => r!.Component)
                .ThenInclude(c => c.Revisions)
            .Include(c => c.ApprovalSteps)
                .ThenInclude(s => s.Decisions)
                .ThenInclude(d => d.Approver)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Engineering change", EngineeringChange.FormatNumber(id));

    private static bool CanManage(EngineeringChange change, User user) =>
        user.Role == UserRole.Admin || (user.Role == UserRole.Engineer && change.RequestedById == user.Id);

    private static ChangeDetailDto ToDetail(EngineeringChange change, User user)
    {
        var canManage = CanManage(change, user);
        var actions = new ChangeActionsDto(
            CanEdit: canManage && change.Status == ChangeStatus.Draft,
            CanSubmit: canManage && change.Status == ChangeStatus.Draft && change.AffectedItems.Count > 0,
            CanApprove: user.Role != UserRole.Viewer && change.GetDecisionBlocker(user.Id, user.Role) is null,
            CanImplement: canManage && change.Status == ChangeStatus.Approved,
            CanCancel: canManage && change.IsOpen);

        return new ChangeDetailDto(
            change.Id,
            change.ChangeNumber,
            change.Title,
            change.Description,
            change.Reason,
            change.Status,
            change.Priority,
            change.RequestedById,
            change.RequestedBy.DisplayName,
            change.WorkflowDefinitionId,
            change.WorkflowDefinition.Name,
            change.CurrentStepOrder,
            change.CreatedAtUtc,
            change.SubmittedAtUtc,
            change.DecidedAtUtc,
            change.ImplementedAtUtc,
            change.AffectedItems.Select(ToAffectedItem).OrderBy(a => a.ItemType).ThenBy(a => a.ItemNumber, StringComparer.Ordinal).ToList(),
            change.ApprovalSteps
                .OrderBy(s => s.StepOrder)
                .Select(s => new ApprovalStepDto(
                    s.Id,
                    s.StepOrder,
                    s.Name,
                    s.ApproverRole,
                    s.RequiredApprovals,
                    s.Status,
                    s.ActivatedAtUtc,
                    s.CompletedAtUtc,
                    s.Decisions
                        .OrderBy(d => d.DecidedAtUtc)
                        .Select(d => new ApprovalDecisionDto(d.Id, d.Approver.DisplayName, d.Outcome, d.Comment, d.DecidedAtUtc))
                        .ToList()))
                .ToList(),
            actions);
    }

    private static AffectedItemDto ToAffectedItem(ChangeAffectedItem item) => item switch
    {
        { ProductRevision: { } p } => new AffectedItemDto(item.Id, AffectedItemType.Product, p.ProductId, p.Product.ProductNumber,
            p.Product.Name, p.Id, p.RevisionCode, p.Status, item.Note),
        { ComponentRevision: { } c } => new AffectedItemDto(item.Id, AffectedItemType.Component, c.ComponentId, c.Component.PartNumber,
            c.Component.Name, c.Id, c.RevisionCode, c.Status, item.Note),
        _ => throw new InvalidOperationException("The affected revision must be loaded.")
    };

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
