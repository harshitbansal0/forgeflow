using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.Domain.Changes;

/// <summary>A product or component revision that an engineering change will release.</summary>
public class ChangeAffectedItem : BaseEntity, IAuditable, IAuditChild
{
    public int EngineeringChangeId { get; set; }
    public EngineeringChange EngineeringChange { get; set; } = null!;
    public int? ProductRevisionId { get; set; }
    public ProductRevision? ProductRevision { get; set; }
    public int? ComponentRevisionId { get; set; }
    public ComponentRevision? ComponentRevision { get; set; }
    public string? Note { get; set; }

    string IAuditChild.ParentEntityType => nameof(Changes.EngineeringChange);
    int IAuditChild.ParentEntityId => EngineeringChangeId;

    public RevisionBase GetRevision() =>
        (RevisionBase?)ProductRevision ?? ComponentRevision
        ?? throw new InvalidOperationException("The affected revision must be loaded.");
}
