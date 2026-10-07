using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Common;

internal static class ServiceExtensions
{
    /// <summary>Loads the signed-in user from the database so role changes apply without waiting for a new token.</summary>
    public static async Task<User> GetActingUserAsync(this IAppDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is { IsActive: true } ? user : throw new ForbiddenException("Your account is inactive.");
    }

    public static IQueryable<ChangeAffectedItem> WhereChangeIsOpen(this IQueryable<ChangeAffectedItem> query) =>
        query.Where(a => a.EngineeringChange.Status == ChangeStatus.Draft
                         || a.EngineeringChange.Status == ChangeStatus.InReview
                         || a.EngineeringChange.Status == ChangeStatus.Approved);

    public static string? TrimToNull(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
