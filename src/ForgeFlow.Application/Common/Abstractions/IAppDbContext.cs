using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Common.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductRevision> ProductRevisions { get; }
    DbSet<BomItem> BomItems { get; }
    DbSet<Component> Components { get; }
    DbSet<ComponentRevision> ComponentRevisions { get; }
    DbSet<EngineeringChange> EngineeringChanges { get; }
    DbSet<ChangeAffectedItem> ChangeAffectedItems { get; }
    DbSet<ChangeApprovalStep> ChangeApprovalSteps { get; }
    DbSet<ApprovalDecision> ApprovalDecisions { get; }
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
