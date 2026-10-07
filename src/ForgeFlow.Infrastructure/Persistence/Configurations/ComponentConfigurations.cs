using ForgeFlow.Domain.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeFlow.Infrastructure.Persistence.Configurations;

internal sealed class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> builder)
    {
        builder.ToTable("Components");
        builder.ConfigureAuditFields();
        builder.Property(c => c.PartNumber).HasMaxLength(40).IsRequired();
        builder.HasIndex(c => c.PartNumber).IsUnique();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Name);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.HasIndex(c => c.Type);
        builder.Property(c => c.Material).HasMaxLength(100);
        builder.Property(c => c.UnitOfMeasure).HasMaxLength(10).IsRequired();
        builder.Property(c => c.Supplier).HasMaxLength(200);
        builder.Property(c => c.UnitCost).HasPrecision(18, 2);
        builder.Property(c => c.LifecycleState).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.HasIndex(c => c.LifecycleState);

        builder.HasMany(c => c.Revisions)
            .WithOne(r => r.Component)
            .HasForeignKey(r => r.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ComponentRevisionConfiguration : IEntityTypeConfiguration<ComponentRevision>
{
    public void Configure(EntityTypeBuilder<ComponentRevision> builder)
    {
        builder.ToTable("ComponentRevisions");
        builder.ConfigureRevision();
        builder.Property(r => r.DrawingNumber).HasMaxLength(60);
        builder.Property(r => r.WeightKg).HasPrecision(10, 3);
        builder.HasIndex(r => new { r.ComponentId, r.RevisionCode }).IsUnique();
    }
}
