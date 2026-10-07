using ForgeFlow.Application.Approvals;
using ForgeFlow.Application.Audit;
using ForgeFlow.Application.Common.Paging;
using ForgeFlow.Application.Common.Security;
using ForgeFlow.Application.Dashboard;
using ForgeFlow.Application.Search;
using ForgeFlow.Application.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeFlow.Api.Controllers;

[ApiController]
[Route("api/approvals")]
[Authorize(Policy = Policies.CanApprove)]
public sealed class ApprovalsController(IApprovalService approvalService) : ControllerBase
{
    [HttpGet("pending")]
    public Task<IReadOnlyList<PendingApprovalDto>> Pending(CancellationToken cancellationToken) =>
        approvalService.GetPendingAsync(cancellationToken);
}

[ApiController]
[Route("api/workflows")]
public sealed class WorkflowsController(IWorkflowService workflowService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WorkflowDto>> List([FromQuery] bool activeOnly, CancellationToken cancellationToken) =>
        workflowService.ListAsync(activeOnly, cancellationToken);

    [HttpGet("{id:int}")]
    public Task<WorkflowDto> Get(int id, CancellationToken cancellationToken) =>
        workflowService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<WorkflowDto>> Create(SaveWorkflowRequest request, CancellationToken cancellationToken)
    {
        var workflow = await workflowService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, workflow);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<WorkflowDto> Update(int id, SaveWorkflowRequest request, CancellationToken cancellationToken) =>
        workflowService.UpdateAsync(id, request, cancellationToken);
}

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = Policies.CanViewAudit)]
public sealed class AuditLogsController(IAuditQueryService auditService) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> List([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
        auditService.ListAsync(query, cancellationToken);
}

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken cancellationToken) => dashboardService.GetAsync(cancellationToken);
}

[ApiController]
[Route("api/search")]
public sealed class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SearchResultDto>> Search([FromQuery] string? q, CancellationToken cancellationToken) =>
        searchService.SearchAsync(q, cancellationToken);
}
