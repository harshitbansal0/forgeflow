using System.Net;
using ForgeFlow.Application.Changes;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Products;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.ApiTests;

public sealed class ChangeWorkflowApiTests(ForgeFlowApiFactory factory) : IClassFixture<ForgeFlowApiFactory>
{
    [Fact]
    public async Task FullApprovalFlow_ReleasesRevision()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var approver = await factory.CreateClientForAsync(ForgeFlowApiFactory.Approver);
        var admin = await factory.CreateClientForAsync(ForgeFlowApiFactory.Admin);
        var product = await CreateProductAsync(engineer);

        var change = await CreateChangeAsync(engineer, product.Id);
        Assert.Equal(ChangeStatus.Draft, change.Status);
        Assert.True(change.AvailableActions.CanSubmit);

        change = await (await engineer.PostAsync($"/api/changes/{change.Id}/submit", null)).ReadAsAsync<ChangeDetailDto>();
        Assert.Equal(ChangeStatus.InReview, change.Status);
        Assert.Equal(1, change.CurrentStepOrder);

        change = await (await approver.PostJsonAsync($"/api/changes/{change.Id}/approve", new DecisionRequest { Comment = "Reviewed" }))
            .ReadAsAsync<ChangeDetailDto>();
        Assert.Equal(2, change.CurrentStepOrder);

        change = await (await admin.PostJsonAsync($"/api/changes/{change.Id}/approve", new DecisionRequest()))
            .ReadAsAsync<ChangeDetailDto>();
        Assert.Equal(ChangeStatus.Approved, change.Status);

        change = await (await engineer.PostAsync($"/api/changes/{change.Id}/implement", null)).ReadAsAsync<ChangeDetailDto>();
        Assert.Equal(ChangeStatus.Implemented, change.Status);

        var released = await (await engineer.GetAsync($"/api/products/{product.Id}")).ReadAsAsync<ProductDetailDto>();
        var revision = Assert.Single(released.Revisions);
        Assert.Equal(RevisionStatus.Released, revision.Status);
        Assert.Equal(change.Id, revision.ReleasedByChangeId);

        var history = await (await engineer.GetAsync($"/api/changes/{change.Id}/history")).ReadAsAsync<List<AuditLogDto>>();
        Assert.Contains(history, h => h.Action == AuditActions.Submitted);
        Assert.Contains(history, h => h.Action == AuditActions.Implemented);
    }

    [Fact]
    public async Task Requester_CannotApproveOwnChange()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var product = await CreateProductAsync(engineer);
        var change = await CreateChangeAsync(engineer, product.Id);
        await engineer.PostAsync($"/api/changes/{change.Id}/submit", null);

        var response = await engineer.PostJsonAsync($"/api/changes/{change.Id}/approve", new DecisionRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WithoutComment_Returns400()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var approver = await factory.CreateClientForAsync(ForgeFlowApiFactory.Approver);
        var product = await CreateProductAsync(engineer);
        var change = await CreateChangeAsync(engineer, product.Id);
        await engineer.PostAsync($"/api/changes/{change.Id}/submit", null);

        var response = await approver.PostJsonAsync($"/api/changes/{change.Id}/reject", new DecisionRequest { Comment = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejection_ReturnsRevisionToDraft()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var approver = await factory.CreateClientForAsync(ForgeFlowApiFactory.Approver);
        var product = await CreateProductAsync(engineer);
        var change = await CreateChangeAsync(engineer, product.Id);
        await engineer.PostAsync($"/api/changes/{change.Id}/submit", null);

        change = await (await approver.PostJsonAsync($"/api/changes/{change.Id}/reject", new DecisionRequest { Comment = "Needs stress analysis" }))
            .ReadAsAsync<ChangeDetailDto>();

        Assert.Equal(ChangeStatus.Rejected, change.Status);
        Assert.Equal(RevisionStatus.Draft, Assert.Single(change.AffectedItems).RevisionStatus);
    }

    [Fact]
    public async Task PendingApprovals_ListsChangesAwaitingTheApproversRole()
    {
        var approver = await factory.CreateClientForAsync(ForgeFlowApiFactory.Approver);
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var product = await CreateProductAsync(engineer);
        var change = await CreateChangeAsync(engineer, product.Id);
        await engineer.PostAsync($"/api/changes/{change.Id}/submit", null);

        var pending = await (await approver.GetAsync("/api/approvals/pending"))
            .ReadAsAsync<List<ForgeFlow.Application.Approvals.PendingApprovalDto>>();

        Assert.Contains(pending, p => p.ChangeId == change.Id);
    }

    [Fact]
    public async Task List_FindsChangeByNumber()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var product = await CreateProductAsync(engineer);
        var change = await CreateChangeAsync(engineer, product.Id);

        var page = await (await engineer.GetAsync($"/api/changes?search={change.ChangeNumber}")).ReadAsAsync<PagedResult<ChangeSummaryDto>>();

        Assert.Equal(change.Id, Assert.Single(page.Items).Id);
    }

    private static async Task<ProductDetailDto> CreateProductAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync("/api/products", new CreateProductRequest
        {
            ProductNumber = HttpContentExtensions.UniqueNumber("FF-ECO"),
            Name = "Workflow test assembly",
            Category = "Test",
            LifecycleState = LifecycleState.Development
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<ProductDetailDto>();
    }

    private static async Task<ChangeDetailDto> CreateChangeAsync(HttpClient client, int productId)
    {
        var response = await client.PostJsonAsync("/api/changes", new CreateChangeRequest
        {
            Title = "Release test assembly",
            Description = "Release revision A for the integration test.",
            Priority = ChangePriority.Medium,
            AffectedItems = [new AffectedItemRequest { ItemType = AffectedItemType.Product, ItemId = productId }]
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsAsync<ChangeDetailDto>();
    }
}
