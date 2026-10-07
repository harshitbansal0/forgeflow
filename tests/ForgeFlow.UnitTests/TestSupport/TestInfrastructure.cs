using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.UnitTests.TestSupport;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public UserRole? Role { get; set; }

    public void SignInAs(User user)
    {
        UserId = user.Id;
        Email = user.Email;
        DisplayName = user.DisplayName;
        Role = user.Role;
    }
}

/// <summary>In-memory SQLite database; the connection stays open so the schema lives for the whole test.</summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public FakeCurrentUser CurrentUser { get; } = new();

    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));

    public ForgeFlowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ForgeFlowDbContext>().UseSqlite(_connection).Options, CurrentUser, Clock);

    public async Task<User> AddUserAsync(string email, UserRole role)
    {
        await using var context = CreateContext();
        var user = new User { Email = email, DisplayName = email.Split('@')[0], Role = role, PasswordHash = "not-a-real-hash" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public void Dispose() => _connection.Dispose();
}
