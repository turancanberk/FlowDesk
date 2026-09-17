using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
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
    private readonly IActivityRecorder _activity;
    private readonly ITenantContext _tenantContext;

    public DeleteTaskHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext, IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _activity = activity;
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

        // Recorded before the row goes; the title exists nowhere else
        // afterwards.
        _activity.Record(
            ActivityType.TaskDeleted,
            ActivitySubject.TaskItem,
            task.Id,
            new TaskActivityPayload(task.Title, task.Status));

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
