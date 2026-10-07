using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeFlow.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public const int UserNameLength = 256;
    public const int EnumLength = 20;

    public static void ConfigureAuditFields<T>(this EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.Property(e => e.CreatedBy).HasMaxLength(UserNameLength).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(UserNameLength);
    }

    public static void ConfigureRevision<T>(this EntityTypeBuilder<T> builder) where T : RevisionBase
    {
        builder.ConfigureAuditFields();
        builder.Property(r => r.RevisionCode).HasMaxLength(4).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(EnumLength);
        builder.Property(r => r.ChangeSummary).HasMaxLength(1000);
        builder.Property(r => r.ReleasedBy).HasMaxLength(UserNameLength);
        builder.HasIndex(r => r.Status);
        builder.Ignore(r => r.IsWorking);

        builder.HasOne<EngineeringChange>()
            .WithMany()
            .HasForeignKey(r => r.EngineeringChangeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
