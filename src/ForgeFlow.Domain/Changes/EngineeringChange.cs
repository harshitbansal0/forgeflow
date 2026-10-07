using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;

namespace ForgeFlow.Domain.Changes;

/// <summary>Engineering change order (ECO): routes draft revisions through approval and releases them.</summary>
public class EngineeringChange : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public ChangePriority Priority { get; set; } = ChangePriority.Medium;
    public ChangeStatus Status { get; private set; } = ChangeStatus.Draft;
    public int RequestedById { get; set; }
    public User RequestedBy { get; set; } = null!;
    public int WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public int? CurrentStepOrder { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public DateTime? ImplementedAtUtc { get; private set; }
    public DateTime? LastActivityAtUtc { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every update.</summary>
    public int Version { get; set; }

    public List<ChangeAffectedItem> AffectedItems { get; set; } = [];
    public List<ChangeApprovalStep> ApprovalSteps { get; set; } = [];

    public string ChangeNumber => FormatNumber(Id);

    public bool IsOpen => Status is ChangeStatus.Draft or ChangeStatus.InReview or ChangeStatus.Approved;

    public static string FormatNumber(int id) => $"ECO-{id:D5}";

    public ChangeApprovalStep? GetActiveStep() =>
        ApprovalSteps.FirstOrDefault(s => s.Status == ApprovalStepStatus.Active);

    public IEnumerable<RevisionBase> GetAffectedRevisions() => AffectedItems.Select(a => a.GetRevision());

    public void EnsureEditable()
    {
        if (Status != ChangeStatus.Draft)
        {
            throw new DomainException($"{ChangeNumber} is {Status}; only draft changes can be edited.");
        }
    }

    public ChangeAffectedItem AddAffectedRevision(RevisionBase revision, string? note)
    {
        EnsureEditable();
        if (revision.Status != RevisionStatus.Draft)
        {
            throw new DomainException($"Revision {revision.RevisionCode} is {revision.Status}; only draft revisions can be added to a change.");
        }

        if (AffectedItems.Any(a => ReferenceEquals(a.GetRevision(), revision)))
        {
            throw new DomainException($"Revision {revision.RevisionCode} is already part of this change.");
        }

        var item = revision switch
        {
            Products.ProductRevision productRevision => new ChangeAffectedItem { ProductRevision = productRevision },
            Components.ComponentRevision componentRevision => new ChangeAffectedItem { ComponentRevision = componentRevision },
            _ => throw new ArgumentException($"Unsupported revision type {revision.GetType().Name}.", nameof(revision))
        };
        item.Note = note;
        AffectedItems.Add(item);
        return item;
    }

    public void RemoveAffectedItem(ChangeAffectedItem item)
    {
        EnsureEditable();
        AffectedItems.Remove(item);
    }

    public void Submit(IReadOnlyCollection<WorkflowStep> workflowSteps, DateTime nowUtc)
    {
        EnsureEditable();
        if (AffectedItems.Count == 0)
        {
            throw new DomainException("Add at least one affected item before submitting.");
        }

        if (workflowSteps.Count == 0)
        {
            throw new DomainException("The selected workflow has no approval steps.");
        }

        foreach (var step in workflowSteps.OrderBy(s => s.StepOrder))
        {
            ApprovalSteps.Add(new ChangeApprovalStep
            {
                StepOrder = step.StepOrder,
                Name = step.Name,
                ApproverRole = step.ApproverRole,
                RequiredApprovals = step.RequiredApprovals
            });
        }

        foreach (var revision in GetAffectedRevisions())
        {
            revision.MarkInReview();
        }

        Status = ChangeStatus.InReview;
        SubmittedAtUtc = nowUtc;
        Activate(ApprovalSteps.OrderBy(s => s.StepOrder).First(), nowUtc);
    }

    /// <summary>Returns why the user can't decide on the active step, or null if they can.</summary>
    public string? GetDecisionBlocker(int userId, UserRole role)
    {
        if (Status != ChangeStatus.InReview)
        {
            return $"{ChangeNumber} is not awaiting approval.";
        }

        var step = GetActiveStep();
        if (step is null)
        {
            return $"{ChangeNumber} has no active approval step.";
        }

        if (userId == RequestedById)
        {
            return "Requesters can't approve their own change.";
        }

        if (role != UserRole.Admin && role != step.ApproverRole)
        {
            return $"Step '{step.Name}' requires the {step.ApproverRole} role.";
        }

        return step.Decisions.Any(d => d.ApproverId == userId)
            ? $"You have already recorded a decision on step '{step.Name}'."
            : null;
    }

    public DecisionResult RecordDecision(int approverId, UserRole approverRole, ApprovalOutcome outcome, string? comment, DateTime nowUtc)
    {
        var blocker = GetDecisionBlocker(approverId, approverRole);
        if (blocker is not null)
        {
            throw new DomainException(blocker);
        }

        if (outcome == ApprovalOutcome.Rejected && string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainException("A comment is required when rejecting a change.");
        }

        var step = GetActiveStep()!;
        step.Decisions.Add(new ApprovalDecision
        {
            ApproverId = approverId,
            Outcome = outcome,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            DecidedAtUtc = nowUtc
        });
        LastActivityAtUtc = nowUtc;

        if (outcome == ApprovalOutcome.Rejected)
        {
            step.Status = ApprovalStepStatus.Rejected;
            step.CompletedAtUtc = nowUtc;
            Close(ChangeStatus.Rejected, nowUtc);
            return DecisionResult.ChangeRejected;
        }

        if (step.Decisions.Count(d => d.Outcome == ApprovalOutcome.Approved) < step.RequiredApprovals)
        {
            return DecisionResult.Recorded;
        }

        step.Status = ApprovalStepStatus.Approved;
        step.CompletedAtUtc = nowUtc;

        var next = ApprovalSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault();
        if (next is not null)
        {
            Activate(next, nowUtc);
            return DecisionResult.StepAdvanced;
        }

        Status = ChangeStatus.Approved;
        DecidedAtUtc = nowUtc;
        CurrentStepOrder = null;
        return DecisionResult.ChangeApproved;
    }

    public IReadOnlyList<RevisionBase> Implement(string implementedBy, DateTime nowUtc)
    {
        if (Status != ChangeStatus.Approved)
        {
            throw new DomainException($"{ChangeNumber} must be approved before it can be implemented.");
        }

        var released = GetAffectedRevisions().ToList();
        foreach (var revision in released)
        {
            revision.Release(Id, implementedBy, nowUtc);
        }

        Status = ChangeStatus.Implemented;
        ImplementedAtUtc = nowUtc;
        LastActivityAtUtc = nowUtc;
        return released;
    }

    public void Cancel(DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new DomainException($"{ChangeNumber} is {Status} and can no longer be cancelled.");
        }

        Close(ChangeStatus.Cancelled, nowUtc);
    }

    private void Activate(ChangeApprovalStep step, DateTime nowUtc)
    {
        step.Status = ApprovalStepStatus.Active;
        step.ActivatedAtUtc = nowUtc;
        CurrentStepOrder = step.StepOrder;
        LastActivityAtUtc = nowUtc;
    }

    private void Close(ChangeStatus status, DateTime nowUtc)
    {
        foreach (var step in ApprovalSteps.Where(s => s.Status is ApprovalStepStatus.Pending or ApprovalStepStatus.Active))
        {
            step.Status = ApprovalStepStatus.Skipped;
        }

        foreach (var revision in GetAffectedRevisions())
        {
            revision.ReturnToDraft();
        }

        Status = status;
        DecidedAtUtc = nowUtc;
        CurrentStepOrder = null;
        LastActivityAtUtc = nowUtc;
    }
}
