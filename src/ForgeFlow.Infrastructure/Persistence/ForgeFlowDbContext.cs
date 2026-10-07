using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using ForgeFlow.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Infrastructure.Persistence;

public sealed class ForgeFlowDbContext(
    DbContextOptions<ForgeFlowDbContext> options,
    ICurrentUser currentUser,
    TimeProvider clock) : DbContext(options), IAppDbContext
{
    public const string SystemActor = "system";

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductRevision> ProductRevisions => Set<ProductRevision>();
    public DbSet<BomItem> BomItems => Set<BomItem>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<ComponentRevision> ComponentRevisions => Set<ComponentRevision>();
    public DbSet<EngineeringChange> EngineeringChanges => Set<EngineeringChange>();
    public DbSet<ChangeAffectedItem> ChangeAffectedItems => Set<ChangeAffectedItem>();
    public DbSet<ChangeApprovalStep> ChangeApprovalSteps => Set<ChangeApprovalStep>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>The demo seeder writes curated history, so it switches automatic capture off.</summary>
    internal bool AuditCaptureEnabled { get; set; } = true;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ForgeFlowDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var actor = currentUser.DisplayName ?? currentUser.Email ?? SystemActor;
        ApplyStamps(now, actor);

        var pending = AuditCaptureEnabled ? AuditCapture.Collect(ChangeTracker) : [];
        if (pending.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // Data and its audit rows commit together; generated keys are only known after the first save.
        await using var transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
        AuditLogs.AddRange(pending.Select(p => p.ToAuditLog(now, currentUser.UserId, actor)));
        await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    private void ApplyStamps(DateTime now, string actor)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default)
                {
                    entry.Entity.CreatedAtUtc = now;
                }

                if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                {
                    entry.Entity.CreatedBy = actor;
                }
            }
            else if (entry.State == EntityState.Modified && AuditCaptureEnabled)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = actor;
            }
        }

        foreach (var entry in ChangeTracker.Entries<EngineeringChange>().Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.Version++;
        }
    }
}
