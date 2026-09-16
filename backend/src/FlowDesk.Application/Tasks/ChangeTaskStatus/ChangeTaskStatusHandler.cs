using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Common;

namespace FlowDesk.Application.Tasks.ChangeTaskStatus;

/// <summary>
/// Moves a task between Todo, InProgress and Done.
/// </summary>
/// <remarks>
/// Its own endpoint so that ticking a checkbox in the list is one request. The
/// full edit replaces every field, including the description, which a list row
/// does not carry — without this, marking something done would mean fetching
/// the task first.
/// </remarks>
public sealed class ChangeTaskStatusHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ChangeTaskStatusHandler(
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
        ChangeTaskStatusCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTasks))
        {
            return Result.Failure<TaskDetail>(
                TenancyErrors.InsufficientRole("Görev durumunu değiştirmek"));
        }

        var task = await TaskWorkflow.FindAsync(_dbContext, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure<TaskDetail>(TaskErrors.NotFound);
        }

        var now = _clock.UtcNow;

        try
        {
            task.ChangeStatus(command.Status, now);
        }
        catch (DomainRuleViolationException)
        {
            // Only reachable with a status outside the enum, which model binding
            // allows through as an undefined value.
            return Result.Failure<TaskDetail>(
                ApplicationError.Validation("task.invalid_status", "Geçerli bir durum seçin."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(
            await TaskWorkflow.ToDetailAsync(_dbContext, _accountStore, task, now, cancellationToken));
    }
}
