using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ForgeFlow.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef`; migrations always target SQL Server regardless of the runtime provider.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ForgeFlowDbContext>
{
    public ForgeFlowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ForgeFlowDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=ForgeFlow;Integrated Security=true;TrustServerCertificate=True")
            .Options;
        return new ForgeFlowDbContext(options, new NoUser(), TimeProvider.System);
    }

    private sealed class NoUser : ICurrentUser
    {
        public int? UserId => null;
        public string? Email => null;
        public string? DisplayName => null;
        public UserRole? Role => null;
    }
}
