using ForgeFlow.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.ConfigureAuditFields();
        builder.Property(p => p.ProductNumber).HasMaxLength(40).IsRequired();
        builder.HasIndex(p => p.ProductNumber).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Name);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Category).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.Category);
        builder.Property(p => p.LifecycleState).HasConversion<string>().HasMaxLength(ConfigurationExtensions.EnumLength);
        builder.HasIndex(p => p.LifecycleState);

        builder.HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Revisions)
            .WithOne(r => r.Product)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductRevisionConfiguration : IEntityTypeConfiguration<ProductRevision>
{
    public void Configure(EntityTypeBuilder<ProductRevision> builder)
    {
        builder.ToTable("ProductRevisions");
        builder.ConfigureRevision();
        builder.HasIndex(r => new { r.ProductId, r.RevisionCode }).IsUnique();

        builder.HasMany(r => r.BomItems)
            .WithOne(i => i.ProductRevision)
            .HasForeignKey(i => i.ProductRevisionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BomItemConfiguration : IEntityTypeConfiguration<BomItem>
{
    public void Configure(EntityTypeBuilder<BomItem> builder)
    {
        builder.ToTable("BomItems");
        builder.Property(i => i.Quantity).HasPrecision(18, 4);
        builder.Property(i => i.ReferenceDesignator).HasMaxLength(100);
        builder.Property(i => i.Notes).HasMaxLength(500);
        builder.HasIndex(i => new { i.ProductRevisionId, i.ComponentId }).IsUnique();

        builder.HasOne(i => i.Component)
            .WithMany()
            .HasForeignKey(i => i.ComponentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
