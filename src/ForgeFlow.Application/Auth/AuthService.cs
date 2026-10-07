using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Auth;

public interface IAuthService
{
    /// <summary>Returns null when the credentials are invalid or the account is inactive.</summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IAuditTrail auditTrail,
    ICurrentUser currentUser,
    TimeProvider clock) : IAuthService
{
    private static string? _timingGuardHash;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Hash anyway so unknown emails take as long as wrong passwords (no account enumeration).
            _timingGuardHash ??= passwordHasher.Hash(Guid.NewGuid().ToString());
            passwordHasher.Verify(_timingGuardHash, request.Password);
            await RecordFailureAsync(null, email, cancellationToken);
            return null;
        }

        if (!passwordHasher.Verify(user.PasswordHash, request.Password) || !user.IsActive)
        {
            await RecordFailureAsync(user, email, cancellationToken);
            return null;
        }

        user.LastLoginAtUtc = clock.GetUtcNow().UtcDateTime;
        auditTrail.RecordFor(user.Id, user.DisplayName, AuditActions.LoginSucceeded, nameof(User), user.Id.ToString(), $"{user.Email} signed in");
        await db.SaveChangesAsync(cancellationToken);

        var token = tokenService.CreateToken(user);
        return new LoginResponse(token.Token, token.ExpiresAtUtc, ToInfo(user));
    }

    public async Task<UserInfo> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
                   ?? throw NotFoundException.For("User", userId);
        return ToInfo(user);
    }

    private Task RecordFailureAsync(User? user, string email, CancellationToken cancellationToken)
    {
        var attempted = email.Length > 256 ? email[..256] : email;
        auditTrail.RecordFor(user?.Id, attempted, AuditActions.LoginFailed, nameof(User), user?.Id.ToString() ?? "-", $"Failed sign-in for {attempted}");
        return db.SaveChangesAsync(cancellationToken);
    }

    private static UserInfo ToInfo(User user) => new(user.Id, user.Email, user.DisplayName, user.Role);
}
