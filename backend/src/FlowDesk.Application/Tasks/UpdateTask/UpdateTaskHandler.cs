using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.Application.Tasks.UpdateTask;

public sealed class UpdateTaskHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public UpdateTaskHandler(
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
        UpdateTaskCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTasks))
        {
            return Result.Failure<TaskDetail>(TenancyErrors.InsufficientRole("Görev düzenlemek"));
        }

        var task = await TaskWorkflow.FindAsync(_dbContext, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure<TaskDetail>(TaskErrors.NotFound);
        }

        var tenantId = _tenantContext.TenantId;

        var customerCheck = await TaskGuards.EnsureCustomerBelongsToWorkspaceAsync(
            _dbContext, tenantId, command.CustomerId, cancellationToken);

        if (customerCheck.IsFailure)
        {
            return Result.Failure<TaskDetail>(customerCheck.Error);
        }

        var assigneeCheck = await TaskGuards.EnsureAssigneeIsMemberAsync(
            _dbContext, tenantId, command.AssignedUserId, cancellationToken);

        if (assigneeCheck.IsFailure)
        {
            return Result.Failure<TaskDetail>(assigneeCheck.Error);
        }

        var now = _clock.UtcNow;

        task.UpdateDetails(command.Title, command.Description, command.DueAt, now);
        task.LinkToCustomer(command.CustomerId, now);
        task.Assign(command.AssignedUserId, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await TaskWorkflow.ToDetailAsync(_dbContext, _accountStore, task, now, cancellationToken));
    }
}
