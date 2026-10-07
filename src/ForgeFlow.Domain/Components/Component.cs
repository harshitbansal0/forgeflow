using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.Domain.Components;

public enum ComponentType
{
    Mechanical,
    Electrical,
    Electronic,
    Hydraulic,
    Software,
    Fastener,
    Material
}

public class Component : AuditableEntity
{
    public string PartNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ComponentType Type { get; set; }
    public string? Material { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public string? Supplier { get; set; }
    public decimal? UnitCost { get; set; }
    public LifecycleState LifecycleState { get; set; } = LifecycleState.Concept;
    public List<ComponentRevision> Revisions { get; set; } = [];

    public ComponentRevision? GetWorkingRevision() => Revisions.FirstOrDefault(r => r.IsWorking);

    public ComponentRevision? GetReleasedRevision() => Revisions.FirstOrDefault(r => r.Status == RevisionStatus.Released);

    /// <summary>Starts the next draft revision, carrying over the released specification.</summary>
    public ComponentRevision Revise(string? changeSummary = null)
    {
        if (LifecycleState == LifecycleState.Obsolete)
        {
            throw new DomainException($"{PartNumber} is obsolete and can't be revised.");
        }

        var working = GetWorkingRevision();
        if (working is not null)
        {
            throw new DomainException($"{PartNumber} already has working revision {working.RevisionCode}.");
        }

        var latest = Revisions.OrderByRevision().LastOrDefault();
        var released = GetReleasedRevision();
        var revision = new ComponentRevision
        {
            RevisionCode = latest is null ? RevisionSequence.Initial : RevisionSequence.Next(latest.RevisionCode),
            ChangeSummary = changeSummary,
            DrawingNumber = released?.DrawingNumber,
            WeightKg = released?.WeightKg,
            Component = this
        };
        Revisions.Add(revision);
        return revision;
    }
}

public class ComponentRevision : RevisionBase, IAuditChild
{
    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;
    public string? DrawingNumber { get; set; }
    public decimal? WeightKg { get; set; }

    string IAuditChild.ParentEntityType => nameof(Components.Component);
    int IAuditChild.ParentEntityId => ComponentId;

    protected override IEnumerable<RevisionBase> GetSiblingRevisions() =>
        (Component ?? throw new InvalidOperationException("The component must be loaded to release its revisions.")).Revisions;
}
