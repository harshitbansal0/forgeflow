using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Infrastructure.Identity;
using ForgeFlow.Infrastructure.Persistence;
using ForgeFlow.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeFlow.Infrastructure;

public static class DependencyInjection
{
    public const string SqlServerProvider = "SqlServer";
    public const string SqliteProvider = "Sqlite";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configuration is read when the context is first built, so test hosts can override it.
        services.AddDbContext<ForgeFlowDbContext>((serviceProvider, options) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var provider = config["Database:Provider"] ?? SqlServerProvider;

            if (provider.Equals(SqlServerProvider, StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(
                    RequireConnectionString(config, SqlServerProvider),
                    sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }
            else if (provider.Equals(SqliteProvider, StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(
                    RequireConnectionString(config, SqliteProvider),
                    sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported Database:Provider '{provider}'. Use '{SqlServerProvider}' or '{SqliteProvider}'.");
            }
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ForgeFlowDbContext>());
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);
    }

    private static string RequireConnectionString(IConfiguration configuration, string name)
    {
        var connectionString = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"ConnectionStrings:{name} is not configured. Set it with user-secrets or an environment variable.")
            : connectionString;
    }
}
