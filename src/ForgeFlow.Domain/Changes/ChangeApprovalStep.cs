using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Domain.Changes;

/// <summary>Snapshot of a workflow step taken at submission, so later workflow edits don't affect changes in flight.</summary>
public class ChangeApprovalStep : BaseEntity
{
    public int EngineeringChangeId { get; set; }
    public EngineeringChange EngineeringChange { get; set; } = null!;
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserRole ApproverRole { get; set; }
    public int RequiredApprovals { get; set; } = 1;
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<ApprovalDecision> Decisions { get; set; } = [];
}

public class ApprovalDecision : BaseEntity
{
    public int ChangeApprovalStepId { get; set; }
    public int ApproverId { get; set; }
    public User Approver { get; set; } = null!;
    public ApprovalOutcome Outcome { get; set; }
    public string? Comment { get; set; }
    public DateTime DecidedAtUtc { get; set; }
}
