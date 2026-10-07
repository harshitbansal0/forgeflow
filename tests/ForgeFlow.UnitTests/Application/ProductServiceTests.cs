using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Application.Products;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using ForgeFlow.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.UnitTests.Application;

public sealed class ProductServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task CreateAsync_CreatesProductWithDraftRevisionA()
    {
        await SignInEngineerAsync();
        await using var context = _database.CreateContext();
        var service = new ProductService(context, _database.CurrentUser);

        var product = await service.CreateAsync(NewProductRequest("ff-act-900"), CancellationToken.None);

        Assert.Equal("FF-ACT-900", product.ProductNumber);
        var revision = Assert.Single(product.Revisions);
        Assert.Equal("A", revision.RevisionCode);
        Assert.Equal(RevisionStatus.Draft, revision.Status);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateNumber_ThrowsConflict()
    {
        await SignInEngineerAsync();
        await using var context = _database.CreateContext();
        var service = new ProductService(context, _database.CurrentUser);
        await service.CreateAsync(NewProductRequest("FF-DUP-1"), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(NewProductRequest("ff-dup-1"), CancellationToken.None));
    }

    [Fact]
    public async Task ReviseAsync_WhileDraftExists_ThrowsDomainException()
    {
        await SignInEngineerAsync();
        await using var context = _database.CreateContext();
        var service = new ProductService(context, _database.CurrentUser);
        var product = await service.CreateAsync(NewProductRequest("FF-REV-1"), CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() => service.ReviseAsync(product.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ReleasedRevision_IsLocked_AndReviseCopiesItsBom()
    {
        var engineer = await SignInEngineerAsync();
        var component = await AddComponentAsync("CMP-7001");
        int productId;
        int revisionAId;

        await using (var context = _database.CreateContext())
        {
            var service = new ProductService(context, _database.CurrentUser);
            var created = await service.CreateAsync(NewProductRequest("FF-BOM-1"), CancellationToken.None);
            productId = created.Id;
            revisionAId = created.Revisions.Single().Id;
            await service.AddBomItemAsync(productId, revisionAId,
                new AddBomItemRequest { ComponentId = component.Id, Quantity = 4m }, CancellationToken.None);
        }

        await ReleaseAsync(productId, engineer);

        await using (var context = _database.CreateContext())
        {
            var service = new ProductService(context, _database.CurrentUser);

            await Assert.ThrowsAsync<DomainException>(() => service.AddBomItemAsync(productId, revisionAId,
                new AddBomItemRequest { ComponentId = component.Id, Quantity = 1m }, CancellationToken.None));

            var revisionB = await service.ReviseAsync(productId, CancellationToken.None);

            Assert.Equal("B", revisionB.Revision.RevisionCode);
            var line = Assert.Single(revisionB.BomItems);
            Assert.Equal(4m, line.Quantity);
        }
    }

    [Fact]
    public async Task CompareRevisionsAsync_ReportsBomDifferences()
    {
        var engineer = await SignInEngineerAsync();
        var kept = await AddComponentAsync("CMP-7101");
        var added = await AddComponentAsync("CMP-7102");
        int productId;
        int revisionAId;

        await using (var context = _database.CreateContext())
        {
            var service = new ProductService(context, _database.CurrentUser);
            var created = await service.CreateAsync(NewProductRequest("FF-CMP-1"), CancellationToken.None);
            productId = created.Id;
            revisionAId = created.Revisions.Single().Id;
            await service.AddBomItemAsync(productId, revisionAId, new AddBomItemRequest { ComponentId = kept.Id, Quantity = 2m }, CancellationToken.None);
        }

        await ReleaseAsync(productId, engineer);

        await using (var context = _database.CreateContext())
        {
            var service = new ProductService(context, _database.CurrentUser);
            var revisionB = await service.ReviseAsync(productId, CancellationToken.None);
            var keptLine = revisionB.BomItems.Single();
            await service.UpdateBomItemAsync(productId, revisionB.Revision.Id, keptLine.Id,
                new UpdateBomItemRequest { Quantity = 3m }, CancellationToken.None);
            await service.AddBomItemAsync(productId, revisionB.Revision.Id,
                new AddBomItemRequest { ComponentId = added.Id, Quantity = 1m }, CancellationToken.None);

            var comparison = await service.CompareRevisionsAsync(productId, revisionAId, revisionB.Revision.Id, CancellationToken.None);

            Assert.Collection(comparison.Differences,
                d => Assert.Equal((kept.PartNumber, BomDifferenceKind.QuantityChanged, (decimal?)2m, (decimal?)3m), (d.PartNumber, d.Kind, d.FromQuantity, d.ToQuantity)),
                d => Assert.Equal((added.PartNumber, BomDifferenceKind.Added), (d.PartNumber, d.Kind)));
        }
    }

    public void Dispose() => _database.Dispose();

    private static CreateProductRequest NewProductRequest(string number) => new()
    {
        ProductNumber = number,
        Name = "Test assembly",
        Category = "Test",
        LifecycleState = LifecycleState.Development
    };

    private async Task<User> SignInEngineerAsync()
    {
        var engineer = await _database.AddUserAsync($"engineer-{Guid.NewGuid():N}@forgeflow.test", UserRole.Engineer);
        _database.CurrentUser.SignInAs(engineer);
        return engineer;
    }

    private async Task<Component> AddComponentAsync(string partNumber)
    {
        await using var context = _database.CreateContext();
        var component = new Component { PartNumber = partNumber, Name = partNumber, Type = ComponentType.Mechanical };
        component.Revise("Initial revision");
        context.Components.Add(component);
        await context.SaveChangesAsync();
        return component;
    }

    /// <summary>Releases the product's working revision through a single-step engineering change.</summary>
    private async Task ReleaseAsync(int productId, User requester)
    {
        await using var context = _database.CreateContext();
        var approver = new User { Email = $"approver-{Guid.NewGuid():N}@forgeflow.test", DisplayName = "Approver", Role = UserRole.Approver, PasswordHash = "x" };
        var workflow = new WorkflowDefinition
        {
            Name = $"Test workflow {Guid.NewGuid():N}",
            Steps = [new WorkflowStep { StepOrder = 1, Name = "Review", ApproverRole = UserRole.Approver }]
        };
        context.AddRange(approver, workflow);
        await context.SaveChangesAsync();

        var product = await context.Products.Include(p => p.Revisions).SingleAsync(p => p.Id == productId);
        var change = new EngineeringChange { Title = "Release", Description = "Release", RequestedById = requester.Id, WorkflowDefinitionId = workflow.Id };
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);
        context.EngineeringChanges.Add(change);
        await context.SaveChangesAsync();

        var now = _database.Clock.GetUtcNow().UtcDateTime;
        change.Submit(workflow.Steps, now);
        change.RecordDecision(approver.Id, UserRole.Approver, ApprovalOutcome.Approved, null, now);
        change.Implement(requester.DisplayName, now);
        await context.SaveChangesAsync();
    }
}
