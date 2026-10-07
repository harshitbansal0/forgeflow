using System.ComponentModel.DataAnnotations;
using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.Application.Components;

public sealed class ComponentQuery : PagedRequest
{
    public ComponentType? Type { get; set; }
    public LifecycleState? LifecycleState { get; set; }
}

public sealed record ComponentSummaryDto(
    int Id,
    string PartNumber,
    string Name,
    ComponentType Type,
    string? Material,
    string UnitOfMeasure,
    string? Supplier,
    decimal? UnitCost,
    LifecycleState LifecycleState,
    string? ReleasedRevision,
    string? WorkingRevision,
    RevisionStatus? WorkingRevisionStatus,
    DateTime UpdatedAtUtc);

public sealed record ComponentDetailDto(
    int Id,
    string PartNumber,
    string Name,
    string? Description,
    ComponentType Type,
    string? Material,
    string UnitOfMeasure,
    string? Supplier,
    decimal? UnitCost,
    LifecycleState LifecycleState,
    DateTime CreatedAtUtc,
    string CreatedBy,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy,
    IReadOnlyList<RevisionDto> Revisions);

public sealed record WhereUsedDto(
    int ProductId,
    string ProductNumber,
    string ProductName,
    int RevisionId,
    string RevisionCode,
    RevisionStatus RevisionStatus,
    decimal Quantity,
    string? ReferenceDesignator);

public class ComponentFields
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [EnumDataType(typeof(ComponentType))]
    public ComponentType Type { get; init; }

    [StringLength(100)]
    public string? Material { get; init; }

    [Required, StringLength(10)]
    public string UnitOfMeasure { get; init; } = "EA";

    [StringLength(200)]
    public string? Supplier { get; init; }

    [Range(0, 10_000_000)]
    public decimal? UnitCost { get; init; }

    [EnumDataType(typeof(LifecycleState))]
    public LifecycleState LifecycleState { get; init; } = LifecycleState.Concept;
}

public sealed class CreateComponentRequest : ComponentFields
{
    [Required, RegularExpression(ItemNumbers.Pattern, ErrorMessage = ItemNumbers.ErrorMessage)]
    public string PartNumber { get; init; } = string.Empty;

    [StringLength(60)]
    public string? DrawingNumber { get; init; }

    [Range(0, 100_000)]
    public decimal? WeightKg { get; init; }
}

public sealed class UpdateComponentRequest : ComponentFields;

public sealed class UpdateComponentRevisionRequest
{
    [StringLength(1000)]
    public string? ChangeSummary { get; init; }

    [StringLength(60)]
    public string? DrawingNumber { get; init; }

    [Range(0, 100_000)]
    public decimal? WeightKg { get; init; }
}
