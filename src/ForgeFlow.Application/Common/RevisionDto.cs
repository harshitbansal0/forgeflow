using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.Application.Common;

public sealed record RevisionDto(
    int Id,
    string RevisionCode,
    RevisionStatus Status,
    string? ChangeSummary,
    string? DrawingNumber,
    decimal? WeightKg,
    DateTime CreatedAtUtc,
    string CreatedBy,
    DateTime? ReleasedAtUtc,
    string? ReleasedBy,
    int? ReleasedByChangeId,
    string? ReleasedByChangeNumber,
    int? OpenChangeId,
    string? OpenChangeNumber)
{
    public static RevisionDto From(RevisionBase revision, int? openChangeId)
    {
        var component = revision as ComponentRevision;
        return new RevisionDto(
            revision.Id,
            revision.RevisionCode,
            revision.Status,
            revision.ChangeSummary,
            component?.DrawingNumber,
            component?.WeightKg,
            revision.CreatedAtUtc,
            revision.CreatedBy,
            revision.ReleasedAtUtc,
            revision.ReleasedBy,
            revision.EngineeringChangeId,
            revision.EngineeringChangeId is { } releasedBy ? EngineeringChange.FormatNumber(releasedBy) : null,
            openChangeId,
            openChangeId is { } open ? EngineeringChange.FormatNumber(open) : null);
    }
}
