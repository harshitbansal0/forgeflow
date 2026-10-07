using System.ComponentModel.DataAnnotations;
using ForgeFlow.Application.Common;
using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Application.Common.Exceptions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace ForgeFlow.Application.Workflows;

public sealed record WorkflowStepDto(int Id, int StepOrder, string Name, UserRole ApproverRole, int RequiredApprovals);

public sealed record WorkflowDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<WorkflowStepDto> Steps,
    int UsageCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed class WorkflowStepRequest
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [EnumDataType(typeof(UserRole))]
    public UserRole ApproverRole { get; init; } = UserRole.Approver;

    [Range(1, 5)]
    public int RequiredApprovals { get; init; } = 1;
}

public sealed class SaveWorkflowRequest
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;

    public bool IsDefault { get; init; }

    [Required, MinLength(1), MaxLength(10)]
    public List<WorkflowStepRequest> Steps { get; init; } = [];
}

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<WorkflowDto> GetAsync(int id, CancellationToken cancellationToken);
    Task<WorkflowDto> CreateAsync(SaveWorkflowRequest request, CancellationToken cancellationToken);
    Task<WorkflowDto> UpdateAsync(int id, SaveWorkflowRequest request, CancellationToken cancellationToken);
}

public sealed class WorkflowService(IAppDbContext db) : IWorkflowService
{
    public async Task<IReadOnlyList<WorkflowDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.WorkflowDefinitions.AsNoTracking().Include(w => w.Steps).AsQueryable();
        if (activeOnly)
        {
            query = query.Where(w => w.IsActive);
        }

        var workflows = await query.OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name).ToListAsync(cancellationToken);
        var usage = await GetUsageCountsAsync(cancellationToken);
        return workflows.Select(w => ToDto(w, usage)).ToList();
    }

    public async Task<WorkflowDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var workflow = await db.WorkflowDefinitions.AsNoTracking()
                           .Include(w => w.Steps)
                           .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
                       ?? throw NotFoundException.For("Workflow", id);
        return ToDto(workflow, await GetUsageCountsAsync(cancellationToken));
    }

    public async Task<WorkflowDto> CreateAsync(SaveWorkflowRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(request, null, cancellationToken);
        var workflow = new WorkflowDefinition();
        await ApplyAsync(workflow, request, cancellationToken);

        db.WorkflowDefinitions.Add(workflow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(workflow.Id, cancellationToken);
    }

    public async Task<WorkflowDto> UpdateAsync(int id, SaveWorkflowRequest request, CancellationToken cancellationToken)
    {
        var workflow = await db.WorkflowDefinitions
                           .Include(w => w.Steps)
                           .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
                       ?? throw NotFoundException.For("Workflow", id);

        await ValidateAsync(request, id, cancellationToken);
        if (workflow.IsDefault && (!request.IsDefault || !request.IsActive))
        {
            throw new ConflictException("Make another workflow the default before changing this one.");
        }

        await ApplyAsync(workflow, request, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task ValidateAsync(SaveWorkflowRequest request, int? existingId, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Steps.Any(s => s.ApproverRole == UserRole.Viewer))
        {
            errors["steps"] = ["Viewers can't be assigned as approvers."];
        }

        if (request.IsDefault && !request.IsActive)
        {
            errors["isDefault"] = ["The default workflow must be active."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var name = request.Name.Trim();
        if (await db.WorkflowDefinitions.AnyAsync(w => w.Name == name && w.Id != existingId, cancellationToken))
        {
            throw new ConflictException($"A workflow named '{name}' already exists.");
        }
    }

    private async Task ApplyAsync(WorkflowDefinition workflow, SaveWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (request.IsDefault && !workflow.IsDefault)
        {
            var currentDefaults = await db.WorkflowDefinitions.Where(w => w.IsDefault).ToListAsync(cancellationToken);
            currentDefaults.ForEach(w => w.IsDefault = false);
        }

        workflow.Name = request.Name.Trim();
        workflow.Description = request.Description.TrimToNull();
        workflow.IsActive = request.IsActive;
        workflow.IsDefault = request.IsDefault;

        // Update steps in place so step ids (and their audit history) survive edits.
        var existing = workflow.Steps.OrderBy(s => s.StepOrder).ToList();
        for (var i = 0; i < request.Steps.Count; i++)
        {
            var source = request.Steps[i];
            var step = i < existing.Count ? existing[i] : new WorkflowStep();
            step.StepOrder = i + 1;
            step.Name = source.Name.Trim();
            step.ApproverRole = source.ApproverRole;
            step.RequiredApprovals = source.RequiredApprovals;
            if (i >= existing.Count)
            {
                workflow.Steps.Add(step);
            }
        }

        foreach (var removed in existing.Skip(request.Steps.Count))
        {
            workflow.Steps.Remove(removed);
        }
    }

    private async Task<Dictionary<int, int>> GetUsageCountsAsync(CancellationToken cancellationToken) =>
        await db.EngineeringChanges.AsNoTracking()
            .GroupBy(c => c.WorkflowDefinitionId)
            .Select(g => new { WorkflowId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkflowId, x => x.Count, cancellationToken);

    private static WorkflowDto ToDto(WorkflowDefinition workflow, IReadOnlyDictionary<int, int> usage) => new(
        workflow.Id,
        workflow.Name,
        workflow.Description,
        workflow.IsActive,
        workflow.IsDefault,
        workflow.Steps
            .OrderBy(s => s.StepOrder)
            .Select(s => new WorkflowStepDto(s.Id, s.StepOrder, s.Name, s.ApproverRole, s.RequiredApprovals))
            .ToList(),
        usage.GetValueOrDefault(workflow.Id),
        workflow.CreatedAtUtc,
        workflow.UpdatedAtUtc);
}
