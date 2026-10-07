using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Changes;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeFlow.Api.Controllers;

[ApiController]
[Route("api/changes")]
public sealed class ChangesController(IEngineeringChangeService changeService, IAuditQueryService auditService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ChangeSummaryDto>> List([FromQuery] ChangeQuery query, CancellationToken cancellationToken) =>
        changeService.ListAsync(query, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<ChangeDetailDto> Get(int id, CancellationToken cancellationToken) =>
        changeService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<ActionResult<ChangeDetailDto>> Create(CreateChangeRequest request, CancellationToken cancellationToken)
    {
        var change = await changeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = change.Id }, change);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ChangeDetailDto> Update(int id, UpdateChangeRequest request, CancellationToken cancellationToken) =>
        changeService.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:int}/affected-items")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ChangeDetailDto> AddAffectedItem(int id, AffectedItemRequest request, CancellationToken cancellationToken) =>
        changeService.AddAffectedItemAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}/affected-items/{affectedItemId:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ChangeDetailDto> RemoveAffectedItem(int id, int affectedItemId, CancellationToken cancellationToken) =>
        changeService.RemoveAffectedItemAsync(id, affectedItemId, cancellationToken);

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ChangeDetailDto> Submit(int id, CancellationToken cancellationToken) =>
        changeService.SubmitAsync(id, cancellationToken);

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.CanApprove)]
    public Task<ChangeDetailDto> Approve(int id, DecisionRequest request, CancellationToken cancellationToken) =>
        changeService.ApproveAsync(id, request, cancellationToken);

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Policies.CanApprove)]
    public Task<ChangeDetailDto> Reject(int id, DecisionRequest request, CancellationToken cancellationToken) =>
        changeService.RejectAsync(id, request, cancellationToken);

    [HttpPost("{id:int}/implement")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ChangeDetailDto> Implement(int id, CancellationToken cancellationToken) =>
        changeService.ImplementAsync(id, cancellationToken);

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<ChangeDetailDto> Cancel(int id, DecisionRequest request, CancellationToken cancellationToken) =>
        changeService.CancelAsync(id, request, cancellationToken);

    [HttpGet("{id:int}/history")]
    public Task<IReadOnlyList<AuditLogDto>> History(int id, CancellationToken cancellationToken) =>
        auditService.GetChangeHistoryAsync(id, cancellationToken);
}
