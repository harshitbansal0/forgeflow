using ForgeFlow.Domain.Common;

namespace ForgeFlow.Domain.Revisions;

public enum RevisionStatus
{
    Draft,
    InReview,
    Released,
    Superseded
}

/// <summary>Shared state machine for product and component revisions (mapped to separate tables).</summary>
public abstract class RevisionBase : AuditableEntity
{
    public string RevisionCode { get; set; } = RevisionSequence.Initial;
    public RevisionStatus Status { get; private set; } = RevisionStatus.Draft;
    public string? ChangeSummary { get; set; }
    public DateTime? ReleasedAtUtc { get; private set; }
    public string? ReleasedBy { get; private set; }

    /// <summary>The engineering change that released this revision.</summary>
    public int? EngineeringChangeId { get; private set; }

    public bool IsWorking => Status is RevisionStatus.Draft or RevisionStatus.InReview;

    protected abstract IEnumerable<RevisionBase> GetSiblingRevisions();

    public void EnsureEditable()
    {
        if (Status != RevisionStatus.Draft)
        {
            throw new DomainException($"Revision {RevisionCode} is {Status} and can no longer be edited.");
        }
    }

    internal void MarkInReview()
    {
        EnsureEditable();
        Status = RevisionStatus.InReview;
    }

    internal void ReturnToDraft()
    {
        if (Status == RevisionStatus.InReview)
        {
            Status = RevisionStatus.Draft;
        }
    }

    internal void Release(int engineeringChangeId, string releasedBy, DateTime nowUtc)
    {
        if (Status != RevisionStatus.InReview)
        {
            throw new DomainException($"Revision {RevisionCode} must be in review before it can be released.");
        }

        foreach (var previous in GetSiblingRevisions().Where(r => r.Status == RevisionStatus.Released && !ReferenceEquals(r, this)))
        {
            previous.Status = RevisionStatus.Superseded;
        }

        Status = RevisionStatus.Released;
        ReleasedAtUtc = nowUtc;
        ReleasedBy = releasedBy;
        EngineeringChangeId = engineeringChangeId;
    }
}

public static class RevisionOrderingExtensions
{
    /// <summary>Orders A, B, ... Y, AA, AB (length first, then letters).</summary>
    public static IOrderedEnumerable<T> OrderByRevision<T>(this IEnumerable<T> revisions) where T : RevisionBase =>
        revisions.OrderBy(r => r.RevisionCode.Length).ThenBy(r => r.RevisionCode, StringComparer.Ordinal);
}
