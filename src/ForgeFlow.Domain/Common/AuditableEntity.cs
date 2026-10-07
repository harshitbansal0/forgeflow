namespace ForgeFlow.Domain.Common;

public abstract class AuditableEntity : BaseEntity, IAuditable
{
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
