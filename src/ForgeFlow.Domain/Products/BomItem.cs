using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;

namespace ForgeFlow.Domain.Products;

public class BomItem : BaseEntity, IAuditable, IAuditChild
{
    public int ProductRevisionId { get; set; }
    public ProductRevision ProductRevision { get; set; } = null!;
    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? ReferenceDesignator { get; set; }
    public string? Notes { get; set; }

    string IAuditChild.ParentEntityType => nameof(Products.ProductRevision);
    int IAuditChild.ParentEntityId => ProductRevisionId;
}
