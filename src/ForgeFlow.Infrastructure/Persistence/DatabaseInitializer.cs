using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeFlow.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ForgeFlowDbContext db,
    DemoDataSeeder seeder,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlServer())
        {
            logger.LogInformation("Applying SQL Server migrations");
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            // Migrations are generated for SQL Server; the SQLite dev database is created from the model.
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (configuration.GetValue<bool>("Seeding:DemoData") && !await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seeding demo data");
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
