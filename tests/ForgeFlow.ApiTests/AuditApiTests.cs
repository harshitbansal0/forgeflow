using System.Net;
using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Products;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Common;

namespace ForgeFlow.ApiTests;

public sealed class AuditApiTests(ForgeFlowApiFactory factory) : IClassFixture<ForgeFlowApiFactory>
{
    [Fact]
    public async Task Viewer_CannotReadAuditLog()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Viewer);

        var response = await client.GetAsync("/api/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatingProduct_IsRecordedWithUserAndValues()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var admin = await factory.CreateClientForAsync(ForgeFlowApiFactory.Admin);
        var number = HttpContentExtensions.UniqueNumber("FF-AUD");
        var created = await engineer.PostJsonAsync("/api/products", new CreateProductRequest
        {
            ProductNumber = number,
            Name = "Audited assembly",
            Category = "Test",
            LifecycleState = LifecycleState.Concept
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var page = await (await admin.GetAsync($"/api/audit-logs?entityType=Product&action=Created&search={number}"))
            .ReadAsAsync<PagedResult<AuditLogDto>>();

        var entry = Assert.Single(page.Items);
        Assert.Equal(AuditActions.Created, entry.Action);
        Assert.Equal("Elena Petrova", entry.UserName);
        Assert.Contains(entry.Changes, c => c.Property == nameof(ProductDetailDto.ProductNumber) && c.NewValue == number);
    }

    [Fact]
    public async Task ProductHistory_IncludesRevisionChanges()
    {
        var engineer = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var product = await (await engineer.PostJsonAsync("/api/products", new CreateProductRequest
        {
            ProductNumber = HttpContentExtensions.UniqueNumber("FF-HIS"),
            Name = "History assembly",
            Category = "Test"
        })).ReadAsAsync<ProductDetailDto>();

        var history = await (await engineer.GetAsync($"/api/products/{product.Id}/history")).ReadAsAsync<List<AuditLogDto>>();

        Assert.Contains(history, h => h.EntityType == "Product");
        Assert.Contains(history, h => h.EntityType == "ProductRevision" && h.ParentEntityId == product.Id.ToString());
    }
}
