using System.Text.Json;
using ForgeFlow.Application.Audit;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Users;
using ForgeFlow.Infrastructure.Persistence.Auditing;
using ForgeFlow.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.UnitTests.Infrastructure;

public sealed class AuditTrailTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task SaveChanges_RecordsCreatedEntryWithValues()
    {
        var owner = await _database.AddUserAsync("owner@forgeflow.test", UserRole.Engineer);
        _database.CurrentUser.SignInAs(owner);

        await using (var context = _database.CreateContext())
        {
            context.Products.Add(new Product { ProductNumber = "FF-AUD-1", Name = "Audited", Category = "Test", OwnerId = owner.Id });
            await context.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var entry = await verify.AuditLogs.SingleAsync(a => a.EntityType == nameof(Product));
        Assert.Equal(AuditActions.Created, entry.Action);
        Assert.Equal(owner.Id, entry.UserId);
        Assert.Equal(owner.DisplayName, entry.UserName);
        Assert.Equal(_database.Clock.Now.UtcDateTime, entry.TimestampUtc);

        var changes = JsonSerializer.Deserialize<List<AuditPropertyChange>>(entry.ChangesJson!, AuditPropertyChange.JsonOptions)!;
        Assert.Contains(changes, c => c is { Property: nameof(Product.ProductNumber), OldValue: null, NewValue: "FF-AUD-1" });
    }

    [Fact]
    public async Task SaveChanges_RecordsOnlyModifiedProperties_AndRollsUpToParent()
    {
        var owner = await _database.AddUserAsync("owner2@forgeflow.test", UserRole.Engineer);
        _database.CurrentUser.SignInAs(owner);
        int revisionId;

        await using (var context = _database.CreateContext())
        {
            var product = new Product { ProductNumber = "FF-AUD-2", Name = "Audited", Category = "Test", OwnerId = owner.Id };
            product.Revise("Initial");
            context.Products.Add(product);
            await context.SaveChangesAsync();
            revisionId = product.Revisions.Single().Id;
        }

        await using (var context = _database.CreateContext())
        {
            var revision = await context.ProductRevisions.SingleAsync(r => r.Id == revisionId);
            revision.ChangeSummary = "Updated summary";
            await context.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var update = await verify.AuditLogs.SingleAsync(a => a.EntityType == nameof(ProductRevision) && a.Action == AuditActions.Updated);
        Assert.Equal(nameof(Product), update.ParentEntityType);
        var change = Assert.Single(JsonSerializer.Deserialize<List<AuditPropertyChange>>(update.ChangesJson!, AuditPropertyChange.JsonOptions)!);
        Assert.Equal(new AuditPropertyChange(nameof(ProductRevision.ChangeSummary), "Initial", "Updated summary"), change);
    }

    [Fact]
    public async Task SaveChanges_MasksPasswordHashes()
    {
        var admin = await _database.AddUserAsync("admin@forgeflow.test", UserRole.Admin);
        _database.CurrentUser.SignInAs(admin);

        await using (var context = _database.CreateContext())
        {
            var user = await context.Users.SingleAsync(u => u.Id == admin.Id);
            user.PasswordHash = "new-secret-hash";
            await context.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var entry = await verify.AuditLogs.SingleAsync(a => a.EntityType == nameof(User) && a.Action == AuditActions.Updated);
        Assert.DoesNotContain("new-secret-hash", entry.ChangesJson);
        Assert.Contains("***", entry.ChangesJson);
    }

    [Fact]
    public async Task AuditTrail_RecordsBusinessEventForCurrentUser()
    {
        var approver = await _database.AddUserAsync("approver@forgeflow.test", UserRole.Approver);
        _database.CurrentUser.SignInAs(approver);

        await using (var context = _database.CreateContext())
        {
            new AuditTrail(context, _database.CurrentUser, _database.Clock)
                .Record(AuditActions.Approved, "EngineeringChange", "42", "ECO-00042 approved");
            await context.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var entry = await verify.AuditLogs.SingleAsync(a => a.Action == AuditActions.Approved);
        Assert.Equal(approver.Id, entry.UserId);
        Assert.Equal("42", entry.EntityId);
    }

    public void Dispose() => _database.Dispose();
}
