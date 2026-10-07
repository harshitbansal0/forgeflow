namespace ForgeFlow.Domain.Common;

/// <summary>Entities whose inserts, updates and deletes are written to the audit log.</summary>
public interface IAuditable;

/// <summary>Lets child-entity audit entries roll up into the history of their parent.</summary>
public interface IAuditChild
{
    string ParentEntityType { get; }
    int ParentEntityId { get; }
}
