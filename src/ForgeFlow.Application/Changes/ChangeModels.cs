using System.ComponentModel.DataAnnotations;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Application.Changes;

public enum AffectedItemType
{
    Product,
    Component
}

public sealed class ChangeQuery : PagedRequest
{
    public ChangeStatus? Status { get; set; }
    public ChangePriority? Priority { get; set; }
    public bool? RequestedByMe { get; set; }
}

public sealed record ChangeSummaryDto(
    int Id,
    string ChangeNumber,
    string Title,
    ChangeStatus Status,
    ChangePriority Priority,
    string RequestedBy,
    string WorkflowName,
    int AffectedItemCount,
    string? CurrentStepName,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AffectedItemDto(
    int Id,
    AffectedItemType ItemType,
    int ItemId,
    string ItemNumber,
    string ItemName,
    int RevisionId,
    string RevisionCode,
    RevisionStatus RevisionStatus,
    string? Note);

public sealed record ApprovalDecisionDto(int Id, string ApproverName, ApprovalOutcome Outcome, string? Comment, DateTime DecidedAtUtc);

public sealed record ApprovalStepDto(
    int Id,
    int StepOrder,
    string Name,
    UserRole ApproverRole,
    int RequiredApprovals,
    ApprovalStepStatus Status,
    DateTime? ActivatedAtUtc,
    DateTime? CompletedAtUtc,
    IReadOnlyList<ApprovalDecisionDto> Decisions);

public sealed record ChangeActionsDto(bool CanEdit, bool CanSubmit, bool CanApprove, bool CanImplement, bool CanCancel);

public sealed record ChangeDetailDto(
    int Id,
    string ChangeNumber,
    string Title,
    string Description,
    string? Reason,
    ChangeStatus Status,
    ChangePriority Priority,
    int RequestedById,
    string RequestedBy,
    int WorkflowDefinitionId,
    string WorkflowName,
    int? CurrentStepOrder,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? DecidedAtUtc,
    DateTime? ImplementedAtUtc,
    IReadOnlyList<AffectedItemDto> AffectedItems,
    IReadOnlyList<ApprovalStepDto> ApprovalSteps,
    ChangeActionsDto AvailableActions);

public sealed class AffectedItemRequest
{
    [EnumDataType(typeof(AffectedItemType))]
    public AffectedItemType ItemType { get; init; }

    [Range(1, int.MaxValue)]
    public int ItemId { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }
}

public class ChangeFields
{
    [Required, StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Reason { get; init; }

    [EnumDataType(typeof(ChangePriority))]
    public ChangePriority Priority { get; init; } = ChangePriority.Medium;
}

public sealed class CreateChangeRequest : ChangeFields
{
    /// <summary>Defaults to the active default workflow.</summary>
    public int? WorkflowDefinitionId { get; init; }

    [MaxLength(50)]
    public List<AffectedItemRequest> AffectedItems { get; init; } = [];
}

public sealed class UpdateChangeRequest : ChangeFields
{
    [Range(1, int.MaxValue)]
    public int WorkflowDefinitionId { get; init; }
}

public sealed class DecisionRequest
{
    [StringLength(2000)]
    public string? Comment { get; init; }
}
