using System.Net;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Products;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Revisions;

namespace ForgeFlow.ApiTests;

public sealed class ProductsApiTests(ForgeFlowApiFactory factory) : IClassFixture<ForgeFlowApiFactory>
{
    [Fact]
    public async Task Viewer_CannotCreateProduct()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Viewer);

        var response = await client.PostJsonAsync("/api/products", NewProduct(HttpContentExtensions.UniqueNumber("FF-VIEW")));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Engineer_CreatesProduct_WithDraftRevisionA()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var number = HttpContentExtensions.UniqueNumber("FF-NEW");

        var response = await client.PostJsonAsync("/api/products", NewProduct(number));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var product = await response.ReadAsAsync<ProductDetailDto>();
        Assert.Equal(number, product.ProductNumber);
        var revision = Assert.Single(product.Revisions);
        Assert.Equal("A", revision.RevisionCode);
        Assert.Equal(RevisionStatus.Draft, revision.Status);
    }

    [Fact]
    public async Task DuplicateProductNumber_Returns409()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);
        var number = HttpContentExtensions.UniqueNumber("FF-DUP");
        await client.PostJsonAsync("/api/products", NewProduct(number));

        var response = await client.PostJsonAsync("/api/products", NewProduct(number.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InvalidProductNumber_Returns400()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Engineer);

        var response = await client.PostJsonAsync("/api/products", NewProduct("!!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_FiltersBySearchTerm()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Viewer);

        var page = await (await client.GetAsync("/api/products?search=FF-ACT&pageSize=10")).ReadAsAsync<PagedResult<ProductSummaryDto>>();

        Assert.Contains(page.Items, p => p.ProductNumber == "FF-ACT-100");
        Assert.All(page.Items, p => Assert.Contains("ACT", p.ProductNumber));
    }

    [Fact]
    public async Task Viewer_CannotDeleteProduct()
    {
        var client = await factory.CreateClientForAsync(ForgeFlowApiFactory.Viewer);

        var response = await client.DeleteAsync("/api/products/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static CreateProductRequest NewProduct(string number) => new()
    {
        ProductNumber = number,
        Name = "API test assembly",
        Category = "Test",
        LifecycleState = LifecycleState.Development
    };
}
