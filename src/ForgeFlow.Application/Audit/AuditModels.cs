using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ForgeFlow.Application.Common.Paging;

namespace ForgeFlow.Application.Audit;

public sealed record AuditPropertyChange(string Property, string? OldValue, string? NewValue)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record AuditLogDto(
    long Id,
    DateTime TimestampUtc,
    int? UserId,
    string UserName,
    string Action,
    string EntityType,
    string EntityId,
    string? ParentEntityType,
    string? ParentEntityId,
    string? Summary,
    IReadOnlyList<AuditPropertyChange> Changes);

public sealed class AuditLogQuery : PagedRequest
{
    [StringLength(60)]
    public string? EntityType { get; set; }

    [StringLength(40)]
    public string? Action { get; set; }

    public int? UserId { get; set; }

    public DateTime? FromUtc { get; set; }

    /// <summary>Exclusive upper bound.</summary>
    public DateTime? ToUtc { get; set; }
}
