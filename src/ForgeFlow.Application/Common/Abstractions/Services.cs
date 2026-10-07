using ForgeFlow.Domain.Users;

namespace ForgeFlow.Application.Common.Abstractions;

public interface ICurrentUser
{
    int? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    UserRole? Role { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateToken(User user);
}

/// <summary>Records business events (submit, approve, release...) alongside the automatic data-change audit.</summary>
public interface IAuditTrail
{
    void Record(
        string action,
        string entityType,
        string entityId,
        string summary,
        string? parentEntityType = null,
        string? parentEntityId = null);

    /// <summary>Records an event for an explicit actor, e.g. sign-in attempts before a user is authenticated.</summary>
    void RecordFor(int? userId, string userName, string action, string entityType, string entityId, string summary);
}
