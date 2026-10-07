using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Audit;

namespace ForgeFlow.Infrastructure.Persistence.Auditing;

/// <summary>Queues business-event audit rows; they are saved with the caller's next SaveChanges.</summary>
public sealed class AuditTrail(ForgeFlowDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditTrail
{
    public void Record(
        string action,
        string entityType,
        string entityId,
        string summary,
        string? parentEntityType = null,
        string? parentEntityId = null) =>
        Add(currentUser.UserId, currentUser.DisplayName ?? currentUser.Email ?? ForgeFlowDbContext.SystemActor,
            action, entityType, entityId, summary, parentEntityType, parentEntityId);

    public void RecordFor(int? userId, string userName, string action, string entityType, string entityId, string summary) =>
        Add(userId, userName, action, entityType, entityId, summary, null, null);

    private void Add(
        int? userId,
        string userName,
        string action,
        string entityType,
        string entityId,
        string summary,
        string? parentEntityType,
        string? parentEntityId) =>
        db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = clock.GetUtcNow().UtcDateTime,
            UserId = userId,
            UserName = Truncate(userName, 256),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ParentEntityType = parentEntityType,
            ParentEntityId = parentEntityId,
            Summary = Truncate(summary, 1000)
        });

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
