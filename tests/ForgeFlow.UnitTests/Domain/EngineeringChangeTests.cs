using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using ForgeFlow.UnitTests.TestSupport;
using static ForgeFlow.UnitTests.TestSupport.DomainBuilder;

namespace ForgeFlow.UnitTests.Domain;

public sealed class EngineeringChangeTests
{
    [Fact]
    public void Submit_WithoutAffectedItems_Throws()
    {
        var change = NewChange();

        var error = Assert.Throws<DomainException>(() => change.Submit(StandardSteps(), Now));

        Assert.Contains("affected item", error.Message);
    }

    [Fact]
    public void Submit_SnapshotsWorkflowAndActivatesFirstStep()
    {
        var product = NewProduct();
        var change = NewChange();
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);

        change.Submit(StandardSteps(), Now);

        Assert.Equal(ChangeStatus.InReview, change.Status);
        Assert.Equal(1, change.CurrentStepOrder);
        Assert.Collection(change.ApprovalSteps.OrderBy(s => s.StepOrder),
            step => Assert.Equal(ApprovalStepStatus.Active, step.Status),
            step => Assert.Equal(ApprovalStepStatus.Pending, step.Status));
        Assert.Equal(RevisionStatus.InReview, product.Revisions.Single().Status);
    }

    [Fact]
    public void Submitted_RevisionsCanNoLongerBeEdited()
    {
        var product = NewProduct();
        var change = NewChange();
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);
        change.Submit(StandardSteps(), Now);

        Assert.Throws<DomainException>(() => product.Revisions.Single().EnsureEditable());
    }

    [Fact]
    public void Requester_CannotApproveOwnChange_EvenAsAdmin()
    {
        var change = SubmittedChange(StandardSteps());

        Assert.NotNull(change.GetDecisionBlocker(RequesterId, UserRole.Admin));
        Assert.Throws<DomainException>(() =>
            change.RecordDecision(RequesterId, UserRole.Admin, ApprovalOutcome.Approved, null, Now));
    }

    [Fact]
    public void Approver_WithWrongRole_IsBlocked()
    {
        var change = SubmittedChange(StandardSteps());

        var blocker = change.GetDecisionBlocker(EngineerId, UserRole.Engineer);

        Assert.Contains("requires the Approver role", blocker);
    }

    [Fact]
    public void Admin_CanApproveAnyStep()
    {
        var change = SubmittedChange(StandardSteps());

        Assert.Null(change.GetDecisionBlocker(AdminId, UserRole.Admin));
    }

    [Fact]
    public void Step_WaitsForRequiredApprovals_AndRejectsDuplicateDecisions()
    {
        var change = SubmittedChange(Steps((UserRole.Approver, 2), (UserRole.Admin, 1)));

        var first = change.RecordDecision(ApproverId, UserRole.Approver, ApprovalOutcome.Approved, null, Now);
        Assert.Equal(DecisionResult.Recorded, first);
        Assert.Equal(1, change.CurrentStepOrder);

        Assert.Throws<DomainException>(() =>
            change.RecordDecision(ApproverId, UserRole.Approver, ApprovalOutcome.Approved, null, Now));

        var second = change.RecordDecision(SecondApproverId, UserRole.Approver, ApprovalOutcome.Approved, null, Now);
        Assert.Equal(DecisionResult.StepAdvanced, second);
        Assert.Equal(2, change.CurrentStepOrder);
    }

    [Fact]
    public void FinalApproval_ApprovesChange()
    {
        var change = SubmittedChange(StandardSteps());

        change.RecordDecision(ApproverId, UserRole.Approver, ApprovalOutcome.Approved, "Looks good", Now);
        var result = change.RecordDecision(AdminId, UserRole.Admin, ApprovalOutcome.Approved, null, Now);

        Assert.Equal(DecisionResult.ChangeApproved, result);
        Assert.Equal(ChangeStatus.Approved, change.Status);
        Assert.Null(change.CurrentStepOrder);
        Assert.All(change.ApprovalSteps, step => Assert.Equal(ApprovalStepStatus.Approved, step.Status));
    }

    [Fact]
    public void Rejection_RequiresComment()
    {
        var change = SubmittedChange(StandardSteps());

        Assert.Throws<DomainException>(() =>
            change.RecordDecision(ApproverId, UserRole.Approver, ApprovalOutcome.Rejected, " ", Now));
    }

    [Fact]
    public void Rejection_ClosesChangeAndReturnsRevisionsToDraft()
    {
        var product = NewProduct();
        var change = NewChange();
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);
        change.Submit(StandardSteps(), Now);

        var result = change.RecordDecision(ApproverId, UserRole.Approver, ApprovalOutcome.Rejected, "Insufficient analysis", Now);

        Assert.Equal(DecisionResult.ChangeRejected, result);
        Assert.Equal(ChangeStatus.Rejected, change.Status);
        Assert.Equal(RevisionStatus.Draft, product.Revisions.Single().Status);
        Assert.Equal(ApprovalStepStatus.Skipped, change.ApprovalSteps.Single(s => s.StepOrder == 2).Status);
    }

    [Fact]
    public void Implement_ReleasesRevisionAndSupersedesPreviousRelease()
    {
        var product = NewProduct();
        Release(product, changeId: 1);
        var revisionA = product.Revisions.Single();

        var revisionB = product.Revise("Second revision");
        var change = Release(product, changeId: 2);

        Assert.Equal(ChangeStatus.Implemented, change.Status);
        Assert.Equal(RevisionStatus.Superseded, revisionA.Status);
        Assert.Equal(RevisionStatus.Released, revisionB.Status);
        Assert.Equal(2, revisionB.EngineeringChangeId);
        Assert.Equal("B", revisionB.RevisionCode);
    }

    [Fact]
    public void Implement_BeforeApproval_Throws()
    {
        var change = SubmittedChange(StandardSteps());

        Assert.Throws<DomainException>(() => change.Implement("Test Engineer", Now));
    }

    [Fact]
    public void Cancel_ReturnsRevisionsToDraft()
    {
        var product = NewProduct();
        var change = NewChange();
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);
        change.Submit(StandardSteps(), Now);

        change.Cancel(Now);

        Assert.Equal(ChangeStatus.Cancelled, change.Status);
        Assert.Equal(RevisionStatus.Draft, product.Revisions.Single().Status);
        Assert.Throws<DomainException>(() => change.Cancel(Now));
    }

    [Fact]
    public void AddAffectedRevision_RejectsDuplicates()
    {
        var product = NewProduct();
        var change = NewChange();
        change.AddAffectedRevision(product.GetWorkingRevision()!, null);

        Assert.Throws<DomainException>(() => change.AddAffectedRevision(product.GetWorkingRevision()!, null));
    }

    [Fact]
    public void Revise_WhileWorkingRevisionExists_Throws()
    {
        var product = NewProduct();

        Assert.Throws<DomainException>(() => product.Revise());
    }

    [Fact]
    public void Revise_CopiesBomFromReleasedRevision()
    {
        var product = NewProduct();
        var bracket = NewComponent(100, "CMP-0100");
        product.GetWorkingRevision()!.AddBomItem(bracket, 3m, "B1-B3", null);
        Release(product);

        var revisionB = product.Revise();

        var line = Assert.Single(revisionB.BomItems);
        Assert.Equal(bracket.Id, line.ComponentId);
        Assert.Equal(3m, line.Quantity);
    }

    private static EngineeringChange SubmittedChange(List<WorkflowStep> steps)
    {
        var change = NewChange();
        change.AddAffectedRevision(NewProduct().GetWorkingRevision()!, null);
        change.Submit(steps, Now);
        return change;
    }
}
