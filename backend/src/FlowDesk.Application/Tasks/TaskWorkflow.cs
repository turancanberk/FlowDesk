using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tasks;

/// <summary>
/// The steps every task write shares: load the task and build its display
/// model with the names a screen needs.
/// </summary>
internal static class TaskWorkflow
{
    /// <summary>
    /// Loads a tracked task in the current workspace, or null.
    /// </summary>
    /// <remarks>
    /// No workspace condition is written here: the global query filter already
    /// scopes the set, so a task belonging to another workspace is simply not
    /// found (ADR-0024). That is why the API can answer 404 rather than 403
    /// without leaking that it exists (ADR-0007).
    /// </remarks>
    public static Task<TaskItem?> FindAsync(
        IFlowDeskDbContext dbContext,
        Guid taskId,
        CancellationToken cancellationToken) =>
        dbContext.Tasks.FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken);

    public static async Task<TaskDetail> ToDetailAsync(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        TaskItem task,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var customerNames = task.CustomerId is { } customerId
            ? await TaskQueries.ResolveCustomerNamesAsync(dbContext, [customerId], cancellationToken)
            : new Dictionary<Guid, string>();

        var userIds = task.AssignedUserId is { } assignedUserId
            ? new[] { task.CreatedByUserId, assignedUserId }.Distinct().ToArray()
            : [task.CreatedByUserId];

        var userNames = await TaskQueries.ResolveUserNamesAsync(
            accountStore, userIds, cancellationToken);

        return new TaskDetail(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.DueAt,
            task.IsOverdue(now),
            task.AssignedUserId,
            task.AssignedUserId is null
                ? null
                : userNames.GetValueOrDefault(task.AssignedUserId.Value, "Bilinmeyen kullanıcı"),
            task.CustomerId,
            task.CustomerId is null
                ? null
                : customerNames.GetValueOrDefault(task.CustomerId.Value, "Bilinmeyen müşteri"),
            task.CreatedByUserId,
            userNames.GetValueOrDefault(task.CreatedByUserId),
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt);
    }
}
