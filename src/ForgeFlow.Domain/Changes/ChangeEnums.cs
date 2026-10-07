namespace ForgeFlow.Domain.Changes;

public enum ChangeStatus
{
    Draft,
    InReview,
    Approved,
    Rejected,
    Implemented,
    Cancelled
}

public enum ChangePriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum ApprovalStepStatus
{
    Pending,
    Active,
    Approved,
    Rejected,
    Skipped
}

public enum ApprovalOutcome
{
    Approved,
    Rejected
}

public enum DecisionResult
{
    Recorded,
    StepAdvanced,
    ChangeApproved,
    ChangeRejected
}
