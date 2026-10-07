using System.ComponentModel.DataAnnotations;
using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.Application.Products;

public sealed class ProductQuery : PagedRequest
{
    public LifecycleState? LifecycleState { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }
}

public sealed record ProductSummaryDto(
    int Id,
    string ProductNumber,
    string Name,
    string Category,
    LifecycleState LifecycleState,
    string OwnerName,
    string? ReleasedRevision,
    string? WorkingRevision,
    RevisionStatus? WorkingRevisionStatus,
    DateTime UpdatedAtUtc);

public sealed record ProductDetailDto(
    int Id,
    string ProductNumber,
    string Name,
    string? Description,
    string Category,
    LifecycleState LifecycleState,
    int OwnerId,
    string OwnerName,
    DateTime CreatedAtUtc,
    string CreatedBy,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy,
    IReadOnlyList<RevisionDto> Revisions);

public sealed record BomItemDto(
    int Id,
    int ComponentId,
    string PartNumber,
    string ComponentName,
    ComponentType ComponentType,
    string UnitOfMeasure,
    decimal Quantity,
    string? ReferenceDesignator,
    string? Notes,
    string? ComponentReleasedRevision,
    LifecycleState ComponentLifecycleState);

public sealed record ProductRevisionDetailDto(
    int ProductId,
    string ProductNumber,
    string ProductName,
    RevisionDto Revision,
    IReadOnlyList<BomItemDto> BomItems);

public sealed record BomDifferenceDto(
    int ComponentId,
    string PartNumber,
    string ComponentName,
    BomDifferenceKind Kind,
    decimal? FromQuantity,
    decimal? ToQuantity);

public sealed record RevisionComparisonDto(
    int FromRevisionId,
    string FromRevision,
    int ToRevisionId,
    string ToRevision,
    IReadOnlyList<BomDifferenceDto> Differences);

public sealed class CreateProductRequest
{
    [Required, RegularExpression(ItemNumbers.Pattern, ErrorMessage = ItemNumbers.ErrorMessage)]
    public string ProductNumber { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Required, StringLength(100)]
    public string Category { get; init; } = string.Empty;

    [EnumDataType(typeof(LifecycleState))]
    public LifecycleState LifecycleState { get; init; } = LifecycleState.Concept;

    /// <summary>Defaults to the signed-in user.</summary>
    public int? OwnerId { get; init; }

    [StringLength(1000)]
    public string? InitialChangeSummary { get; init; }
}

public sealed class UpdateProductRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Required, StringLength(100)]
    public string Category { get; init; } = string.Empty;

    [EnumDataType(typeof(LifecycleState))]
    public LifecycleState LifecycleState { get; init; }

    [Range(1, int.MaxValue)]
    public int OwnerId { get; init; }
}

public sealed class UpdateRevisionRequest
{
    [StringLength(1000)]
    public string? ChangeSummary { get; init; }
}

public sealed class AddBomItemRequest
{
    [Range(1, int.MaxValue)]
    public int ComponentId { get; init; }

    [Range(0.0001, 1_000_000)]
    public decimal Quantity { get; init; } = 1;

    [StringLength(100)]
    public string? ReferenceDesignator { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}

public sealed class UpdateBomItemRequest
{
    [Range(0.0001, 1_000_000)]
    public decimal Quantity { get; init; }

    [StringLength(100)]
    public string? ReferenceDesignator { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }
}
