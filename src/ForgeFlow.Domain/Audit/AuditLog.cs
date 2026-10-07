namespace ForgeFlow.Domain.Audit;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? ParentEntityType { get; set; }
    public string? ParentEntityId { get; set; }
    public string? Summary { get; set; }

    /// <summary>JSON array of { property, oldValue, newValue } for data changes.</summary>
    public string? ChangesJson { get; set; }
}

public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Implemented = "Implemented";
    public const string Released = "Released";
    public const string Cancelled = "Cancelled";
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";

    public static readonly IReadOnlyList<string> All =
    [
        Created, Updated, Deleted, Submitted, Approved, Rejected, Implemented, Released, Cancelled, LoginSucceeded, LoginFailed
    ];
}
