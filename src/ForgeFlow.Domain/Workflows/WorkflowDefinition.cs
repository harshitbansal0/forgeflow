using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Domain.Workflows;

public class WorkflowDefinition : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public List<WorkflowStep> Steps { get; set; } = [];
}

public class WorkflowStep : BaseEntity, IAuditable, IAuditChild
{
    public int WorkflowDefinitionId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public UserRole ApproverRole { get; set; }
    public int RequiredApprovals { get; set; } = 1;

    string IAuditChild.ParentEntityType => nameof(WorkflowDefinition);
    int IAuditChild.ParentEntityId => WorkflowDefinitionId;
}
