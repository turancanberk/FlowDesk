using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.Application.Tasks.DeleteTask;

/// <summary>
/// Removes a task for good.
/// </summary>
/// <remarks>
/// A real delete, and an agent's own to make. A task is internal: it carries no
/// customer conversation and nothing outside the team refers to it. Reserving
/// this for admins would only mean cancelled work stays in everyone's list.
/// </remarks>
public sealed class DeleteTaskHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public DeleteTaskHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.DeleteTasks))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Görev silmek"));
        }

        var task = await TaskWorkflow.FindAsync(_dbContext, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(TaskErrors.NotFound);
        }

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
