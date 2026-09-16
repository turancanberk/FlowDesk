using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.Application.Tasks.GetTask;

public sealed class GetTaskHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public GetTaskHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<TaskDetail>> HandleAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTasks))
        {
            return Result.Failure<TaskDetail>(TenancyErrors.InsufficientRole("Görev görüntülemek"));
        }

        var task = await TaskWorkflow.FindAsync(_dbContext, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure<TaskDetail>(TaskErrors.NotFound);
        }

        return Result.Success(await TaskWorkflow.ToDetailAsync(
            _dbContext, _accountStore, task, _clock.UtcNow, cancellationToken));
    }
}
