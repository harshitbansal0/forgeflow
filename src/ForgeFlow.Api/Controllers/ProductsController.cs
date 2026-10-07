using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Common.Security;
using ForgeFlow.Application.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(IProductService productService, IAuditQueryService auditService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ProductSummaryDto>> List([FromQuery] ProductQuery query, CancellationToken cancellationToken) =>
        productService.ListAsync(query, cancellationToken);

    [HttpGet("categories")]
    public Task<IReadOnlyList<string>> Categories(CancellationToken cancellationToken) =>
        productService.GetCategoriesAsync(cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ProductDetailDto> Get(int id, CancellationToken cancellationToken) =>
        productService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<ActionResult<ProductDetailDto>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ProductDetailDto> Update(int id, UpdateProductRequest request, CancellationToken cancellationToken) =>
        productService.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/revisions")]
    public async Task<IReadOnlyList<RevisionDto>> Revisions(int id, CancellationToken cancellationToken) =>
        (await productService.GetAsync(id, cancellationToken)).Revisions;

    [HttpPost("{id:int}/revisions")]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<ActionResult<ProductRevisionDetailDto>> Revise(int id, CancellationToken cancellationToken)
    {
        var revision = await productService.ReviseAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetRevision), new { id, revisionId = revision.Revision.Id }, revision);
    }

    [HttpGet("{id:int}/revisions/{revisionId:int}")]
    public Task<ProductRevisionDetailDto> GetRevision(int id, int revisionId, CancellationToken cancellationToken) =>
        productService.GetRevisionAsync(id, revisionId, cancellationToken);

    [HttpPut("{id:int}/revisions/{revisionId:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ProductRevisionDetailDto> UpdateRevision(
        int id, int revisionId, UpdateRevisionRequest request, CancellationToken cancellationToken) =>
        productService.UpdateRevisionAsync(id, revisionId, request, cancellationToken);

    [HttpPost("{id:int}/revisions/{revisionId:int}/bom")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ProductRevisionDetailDto> AddBomItem(
        int id, int revisionId, AddBomItemRequest request, CancellationToken cancellationToken) =>
        productService.AddBomItemAsync(id, revisionId, request, cancellationToken);

    [HttpPut("{id:int}/revisions/{revisionId:int}/bom/{bomItemId:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ProductRevisionDetailDto> UpdateBomItem(
        int id, int revisionId, int bomItemId, UpdateBomItemRequest request, CancellationToken cancellationToken) =>
        productService.UpdateBomItemAsync(id, revisionId, bomItemId, request, cancellationToken);

    [HttpDelete("{id:int}/revisions/{revisionId:int}/bom/{bomItemId:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ProductRevisionDetailDto> RemoveBomItem(int id, int revisionId, int bomItemId, CancellationToken cancellationToken) =>
        productService.RemoveBomItemAsync(id, revisionId, bomItemId, cancellationToken);

    [HttpGet("{id:int}/revisions/compare")]
    public Task<RevisionComparisonDto> Compare(int id, [FromQuery] int from, [FromQuery] int to, CancellationToken cancellationToken) =>
        productService.CompareRevisionsAsync(id, from, to, cancellationToken);

    [HttpGet("{id:int}/history")]
    public Task<IReadOnlyList<AuditLogDto>> History(int id, CancellationToken cancellationToken) =>
        auditService.GetProductHistoryAsync(id, cancellationToken);
}
