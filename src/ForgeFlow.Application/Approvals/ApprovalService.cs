using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Approvals;

public sealed record PendingApprovalDto(
    int ChangeId,
    string ChangeNumber,
    string Title,
    ChangePriority Priority,
    string RequestedBy,
    string StepName,
    int StepOrder,
    int TotalSteps,
    int ApprovalsReceived,
    int RequiredApprovals,
    DateTime? SubmittedAtUtc,
    DateTime? StepActivatedAtUtc);

public interface IApprovalService
{
    Task<IReadOnlyList<PendingApprovalDto>> GetPendingAsync(CancellationToken cancellationToken);
    Task<int> CountPendingAsync(CancellationToken cancellationToken);
}

public sealed class ApprovalService(IAppDbContext db, ICurrentUser currentUser) : IApprovalService
{
    public async Task<IReadOnlyList<PendingApprovalDto>> GetPendingAsync(CancellationToken cancellationToken)
    {
        var user = await db.GetActingUserAsync(currentUser, cancellationToken);
        if (user.Role == UserRole.Viewer)
        {
            return [];
        }

        var rows = await PendingStepsFor(user)
            .Select(s => new
            {
                s.EngineeringChangeId,
                s.EngineeringChange.Title,
                s.EngineeringChange.Priority,
                RequestedBy = s.EngineeringChange.RequestedBy.DisplayName,
                s.Name,
                s.StepOrder,
                TotalSteps = s.EngineeringChange.ApprovalSteps.Count,
                ApprovalsReceived = s.Decisions.Count(d => d.Outcome == ApprovalOutcome.Approved),
                s.RequiredApprovals,
                s.EngineeringChange.SubmittedAtUtc,
                s.ActivatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.ActivatedAtUtc)
            .Select(r => new PendingApprovalDto(
                r.EngineeringChangeId,
                EngineeringChange.FormatNumber(r.EngineeringChangeId),
                r.Title,
                r.Priority,
                r.RequestedBy,
                r.Name,
                r.StepOrder,
                r.TotalSteps,
                r.ApprovalsReceived,
                r.RequiredApprovals,
                r.SubmittedAtUtc,
                r.ActivatedAtUtc))
            .ToList();
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken)
    {
        var user = await db.GetActingUserAsync(currentUser, cancellationToken);
        return user.Role == UserRole.Viewer ? 0 : await PendingStepsFor(user).CountAsync(cancellationToken);
    }

    /// <summary>Mirrors <see cref="EngineeringChange.GetDecisionBlocker"/> as a database query.</summary>
    private IQueryable<ChangeApprovalStep> PendingStepsFor(User user)
    {
        var userId = user.Id;
        var role = user.Role;
        var steps = db.ChangeApprovalSteps.AsNoTracking()
            .Where(s => s.Status == ApprovalStepStatus.Active && s.EngineeringChange.Status == ChangeStatus.InReview)
            .Where(s => s.EngineeringChange.RequestedById != userId)
            .Where(s => !s.Decisions.Any(d => d.ApproverId == userId));

        return role == UserRole.Admin ? steps : steps.Where(s => s.ApproverRole == role);
    }
}
