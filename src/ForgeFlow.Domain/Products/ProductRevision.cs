using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.Domain.Products;

public class ProductRevision : RevisionBase, IAuditChild
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public List<BomItem> BomItems { get; set; } = [];

    string IAuditChild.ParentEntityType => nameof(Products.Product);
    int IAuditChild.ParentEntityId => ProductId;

    protected override IEnumerable<RevisionBase> GetSiblingRevisions() =>
        (Product ?? throw new InvalidOperationException("The product must be loaded to release its revision.")).Revisions;

    public BomItem AddBomItem(Component component, decimal quantity, string? referenceDesignator, string? notes)
    {
        EnsureEditable();
        if (component.LifecycleState == LifecycleState.Obsolete)
        {
            throw new DomainException($"{component.PartNumber} is obsolete and can't be added to a BOM.");
        }

        if (BomItems.Any(i => i.ComponentId == component.Id))
        {
            throw new DomainException($"{component.PartNumber} is already on this BOM; update its quantity instead.");
        }

        var item = new BomItem
        {
            Component = component,
            ComponentId = component.Id,
            Quantity = EnsurePositive(quantity),
            ReferenceDesignator = referenceDesignator,
            Notes = notes
        };
        BomItems.Add(item);
        return item;
    }

    public void UpdateBomItem(BomItem item, decimal quantity, string? referenceDesignator, string? notes)
    {
        EnsureEditable();
        item.Quantity = EnsurePositive(quantity);
        item.ReferenceDesignator = referenceDesignator;
        item.Notes = notes;
    }

    public void RemoveBomItem(BomItem item)
    {
        EnsureEditable();
        BomItems.Remove(item);
    }

    private static decimal EnsurePositive(decimal quantity) =>
        quantity > 0 ? quantity : throw new DomainException("Quantity must be greater than zero.");
}
