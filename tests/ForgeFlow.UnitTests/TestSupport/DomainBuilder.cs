using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;

namespace ForgeFlow.UnitTests.TestSupport;

/// <summary>Builds in-memory domain objects without a database.</summary>
public static class DomainBuilder
{
    public const int RequesterId = 1;
    public const int ApproverId = 2;
    public const int SecondApproverId = 3;
    public const int AdminId = 4;
    public const int EngineerId = 5;

    public static readonly DateTime Now = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    public static Product NewProduct(string number = "FF-TEST-1")
    {
        var product = new Product { Id = 10, ProductNumber = number, Name = "Test assembly", Category = "Test", OwnerId = RequesterId };
        product.Revise("Initial revision");
        return product;
    }

    public static Component NewComponent(int id, string partNumber)
    {
        var component = new Component { Id = id, PartNumber = partNumber, Name = partNumber, Type = ComponentType.Mechanical };
        component.Revise("Initial revision");
        return component;
    }

    public static List<WorkflowStep> Steps(params (UserRole Role, int RequiredApprovals)[] steps) =>
        steps.Select((s, i) => new WorkflowStep
        {
            StepOrder = i + 1,
            Name = $"Step {i + 1}",
            ApproverRole = s.Role,
            RequiredApprovals = s.RequiredApprovals
        }).ToList();

    public static List<WorkflowStep> StandardSteps() => Steps((UserRole.Approver, 1), (UserRole.Admin, 1));

    public static EngineeringChange NewChange(int id = 7) =>
        new() { Id = id, Title = "Test change", Description = "Test", RequestedById = RequesterId, WorkflowDefinitionId = 1 };

    /// <summary>Takes a product's working revision through submit, approval and implementation.</summary>
    public static EngineeringChange Release(Product product, int changeId = 7)
    {
        var change = NewChange(changeId);
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);
        change.Submit(Steps((UserRole.Approver, 1)), Now);
        change.RecordDecision(ApproverId, UserRole.Approver, ApprovalOutcome.Approved, null, Now);
        change.Implement("Test Engineer", Now);
        return change;
    }
}
