using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(UserQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserDirectoryEntry>> GetDirectoryAsync(CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);
}

public sealed class UserService(IAppDbContext db, IPasswordHasher passwordHasher, ICurrentUser currentUser) : IUserService
{
    private static readonly SortMap<User> Sorts = new SortMap<User>("displayName")
        .Add("displayName", u => u.DisplayName)
        .Add("email", u => u.Email)
        .Add("role", u => u.Role)
        .Add("lastLogin", u => u.LastLoginAtUtc)
        .Add("createdAt", u => u.CreatedAtUtc);

    public async Task<PagedResult<UserDto>> ListAsync(UserQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();
        if (SearchPattern.Contains(query.Search) is { } pattern)
        {
            users = users.Where(u => EF.Functions.Like(u.DisplayName, pattern, SearchPattern.EscapeCharacter)
                                     || EF.Functions.Like(u.Email, pattern, SearchPattern.EscapeCharacter));
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        return await Sorts.Apply(users, query)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.LastLoginAtUtc, u.CreatedAtUtc))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<UserDirectoryEntry>> GetDirectoryAsync(CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserDirectoryEntry(u.Id, u.DisplayName, u.Role))
            .ToListAsync(cancellationToken);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException($"A user with email {email} already exists.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
                   ?? throw NotFoundException.For("User", id);

        var losesAdmin = user is { Role: UserRole.Admin, IsActive: true } && (request.Role != UserRole.Admin || !request.IsActive);
        if (losesAdmin)
        {
            if (user.Id == currentUser.UserId)
            {
                throw new ConflictException("You can't remove your own administrator access.");
            }

            var otherAdmins = await db.Users.CountAsync(u => u.Id != id && u.Role == UserRole.Admin && u.IsActive, cancellationToken);
            if (otherAdmins == 0)
            {
                throw new ConflictException("At least one active administrator is required.");
            }
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.LastLoginAtUtc, u.CreatedAtUtc);
}
