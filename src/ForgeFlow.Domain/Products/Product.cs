using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;

namespace ForgeFlow.Domain.Products;

public class Product : AuditableEntity
{
    public string ProductNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public LifecycleState LifecycleState { get; set; } = LifecycleState.Concept;
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public List<ProductRevision> Revisions { get; set; } = [];

    public ProductRevision? GetWorkingRevision() => Revisions.FirstOrDefault(r => r.IsWorking);

    public ProductRevision? GetReleasedRevision() =>
        Revisions.FirstOrDefault(r => r.Status == RevisionStatus.Released);

    /// <summary>Starts the next revision, carrying over the BOM of the released revision.</summary>
    public ProductRevision Revise(string? changeSummary = null)
    {
        if (LifecycleState == LifecycleState.Obsolete)
        {
            throw new DomainException($"{ProductNumber} is obsolete and can't be revised.");
        }

        if (GetWorkingRevision() is { } working)
        {
            throw new DomainException($"{ProductNumber} already has working revision {working.RevisionCode}.");
        }

        var latest = Revisions.OrderByRevision().LastOrDefault();
        var basis = GetReleasedRevision();
        var revision = new ProductRevision
        {
            Product = this,
            RevisionCode = latest is null ? RevisionSequence.Initial : RevisionSequence.Next(latest.RevisionCode),
            ChangeSummary = changeSummary,
            BomItems = basis?.BomItems
                .Select(item => new BomItem
                {
                    ComponentId = item.ComponentId,
                    Quantity = item.Quantity,
                    ReferenceDesignator = item.ReferenceDesignator,
                    Notes = item.Notes
                })
                .ToList() ?? []
        };

        Revisions.Add(revision);
        return revision;
    }
}
