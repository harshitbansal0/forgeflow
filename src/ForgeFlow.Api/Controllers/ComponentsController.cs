using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Common.Security;
using ForgeFlow.Application.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeFlow.Api.Controllers;

[ApiController]
[Route("api/components")]
public sealed class ComponentsController(IComponentService componentService, IAuditQueryService auditService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ComponentSummaryDto>> List([FromQuery] ComponentQuery query, CancellationToken cancellationToken) =>
        componentService.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ComponentDetailDto> Get(int id, CancellationToken cancellationToken) =>
        componentService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<ActionResult<ComponentDetailDto>> Create(CreateComponentRequest request, CancellationToken cancellationToken)
    {
        var component = await componentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = component.Id }, component);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ComponentDetailDto> Update(int id, UpdateComponentRequest request, CancellationToken cancellationToken) =>
        componentService.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await componentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/revisions")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ComponentDetailDto> Revise(int id, CancellationToken cancellationToken) =>
        componentService.ReviseAsync(id, cancellationToken);

    [HttpPut("{id:int}/revisions/{revisionId:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ComponentDetailDto> UpdateRevision(
        int id, int revisionId, UpdateComponentRevisionRequest request, CancellationToken cancellationToken) =>
        componentService.UpdateRevisionAsync(id, revisionId, request, cancellationToken);

    [HttpGet("{id:int}/where-used")]
    public Task<IReadOnlyList<WhereUsedDto>> WhereUsed(int id, CancellationToken cancellationToken) =>
        componentService.GetWhereUsedAsync(id, cancellationToken);

    [HttpGet("{id:int}/history")]
    public Task<IReadOnlyList<AuditLogDto>> History(int id, CancellationToken cancellationToken) =>
        auditService.GetComponentHistoryAsync(id, cancellationToken);
}
