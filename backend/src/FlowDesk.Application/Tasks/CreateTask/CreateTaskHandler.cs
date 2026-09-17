using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tasks;

namespace FlowDesk.Application.Tasks.CreateTask;

public sealed class CreateTaskHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public CreateTaskHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext,
        IClock clock,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
        _clock = clock;
        _activity = activity;
    }

    public async Task<Result<TaskDetail>> HandleAsync(
        CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTasks))
        {
            return Result.Failure<TaskDetail>(TenancyErrors.InsufficientRole("Görev oluşturmak"));
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

        var task = TaskItem.Create(
            tenantId,
            command.Title,
            command.Description,
            command.CustomerId,
            command.AssignedUserId,
            command.DueAt,
            _tenantContext.UserId,
            now);

        _dbContext.Tasks.Add(task);
        _activity.Record(
            ActivityType.TaskCreated,
            ActivitySubject.TaskItem,
            task.Id,
            new TaskActivityPayload(task.Title, task.Status));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await TaskWorkflow.ToDetailAsync(_dbContext, _accountStore, task, now, cancellationToken));
    }
}
