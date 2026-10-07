using ForgeFlow.Domain.Changes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeFlow.Infrastructure.Persistence.Configurations;

internal sealed class EngineeringChangeConfiguration : IEntityTypeConfiguration<EngineeringChange>
{
    public void Configure(EntityTypeBuilder<EngineeringChange> builder)
    {
        builder.ToTable("EngineeringChanges");
        builder.ConfigureAuditFields();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(4000).IsRequired();
        builder.Property(c => c.Reason).HasMaxLength(2000);
        builder.Property(c => c.Priority).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.HasIndex(c => c.Status);
        builder.Property(c => c.Version).IsConcurrencyToken();
        builder.Ignore(c => c.ChangeNumber);
        builder.Ignore(c => c.IsOpen);

        builder.HasOne(c => c.RequestedBy)
            .WithMany()
            .HasForeignKey(c => c.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.WorkflowDefinition)
            .WithMany()
            .HasForeignKey(c => c.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.AffectedItems)
            .WithOne(a => a.EngineeringChange)
            .HasForeignKey(a => a.EngineeringChangeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.ApprovalSteps)
            .WithOne(s => s.EngineeringChange)
            .HasForeignKey(s => s.EngineeringChangeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChangeAffectedItemConfiguration : IEntityTypeConfiguration<ChangeAffectedItem>
{
    public void Configure(EntityTypeBuilder<ChangeAffectedItem> builder)
    {
        builder.ToTable("ChangeAffectedItems", table => table.HasCheckConstraint(
            "CK_ChangeAffectedItems_OneRevision",
            "(ProductRevisionId IS NOT NULL AND ComponentRevisionId IS NULL) OR (ProductRevisionId IS NULL AND ComponentRevisionId IS NOT NULL)"));
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.HasIndex(a => new { a.EngineeringChangeId, a.ProductRevisionId }).IsUnique();
        builder.HasIndex(a => new { a.EngineeringChangeId, a.ComponentRevisionId }).IsUnique();

        builder.HasOne(a => a.ProductRevision)
            .WithMany()
            .HasForeignKey(a => a.ProductRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ComponentRevision)
            .WithMany()
            .HasForeignKey(a => a.ComponentRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChangeApprovalStepConfiguration : IEntityTypeConfiguration<ChangeApprovalStep>
{
    public void Configure(EntityTypeBuilder<ChangeApprovalStep> builder)
    {
        builder.ToTable("ChangeApprovalSteps");
        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.Property(s => s.ApproverRole).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.HasIndex(s => new { s.EngineeringChangeId, s.StepOrder }).IsUnique();
        builder.HasIndex(s => new { s.Status, s.ApproverRole });

        builder.HasMany(s => s.Decisions)
            .WithOne()
            .HasForeignKey(d => d.ChangeApprovalStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("ApprovalDecisions");
        builder.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.Property(d => d.Comment).HasMaxLength(2000);
        builder.HasIndex(d => new { d.ChangeApprovalStepId, d.ApproverId }).IsUnique();

        builder.HasOne(d => d.Approver)
            .WithMany()
            .HasForeignKey(d => d.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
