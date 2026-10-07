using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeFlow.Infrastructure.Persistence.Configurations;

internal sealed class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");
        builder.ConfigureAuditFields();
        builder.Property(w => w.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(w => w.Name).IsUnique();
        builder.Property(w => w.Description).HasMaxLength(1000);

        builder.HasMany(w => w.Steps)
            .WithOne()
            .HasForeignKey(s => s.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps");
        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.Property(s => s.ApproverRole).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.HasIndex(s => new { s.WorkflowDefinitionId, s.StepOrder }).IsUnique();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(a => a.UserName).HasMaxLength(ConfigurationExtensions.UserNameLength).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(40).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(60).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(40).IsRequired();
        builder.Property(a => a.ParentEntityType).HasMaxLength(60);
        builder.Property(a => a.ParentEntityId).HasMaxLength(40);
        builder.Property(a => a.Summary).HasMaxLength(1000);
        builder.HasIndex(a => a.TimestampUtc);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => new { a.ParentEntityType, a.ParentEntityId });
        builder.HasIndex(a => a.UserId);
    }
}
