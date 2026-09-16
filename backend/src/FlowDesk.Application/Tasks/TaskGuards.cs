using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tasks;

/// <summary>
/// Checks that the records a task points at belong to the same workspace.
/// </summary>
/// <remarks>
/// The third isolation layer, as for tickets: the query filter scopes what a
/// request can read, but nothing stops a client from sending another
/// workspace's customer or user id in a request body (docs/SECURITY.md).
///
/// Both references are optional here, so null passes — that is "no customer"
/// and "unassigned", not a missing check.
/// </remarks>
internal static class TaskGuards
{
    public static async Task<Result> EnsureCustomerBelongsToWorkspaceAsync(
        IFlowDeskDbContext dbContext,
        Guid tenantId,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        if (customerId is null)
        {
            return Result.Success();
        }

        // IgnoreQueryFilters: an archived customer may still have open work
        // against it, and refusing would strand the task.
        var exists = await dbContext.Customers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                customer => customer.Id == customerId.Value && customer.TenantId == tenantId,
                cancellationToken);

        return exists ? Result.Success() : Result.Failure(TaskErrors.CustomerNotFound);
    }

    public static async Task<Result> EnsureAssigneeIsMemberAsync(
        IFlowDeskDbContext dbContext,
        Guid tenantId,
        Guid? assignedUserId,
        CancellationToken cancellationToken)
    {
        if (assignedUserId is null)
        {
            return Result.Success();
        }

        var isMember = await dbContext.Memberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.TenantId == tenantId
                    && membership.UserId == assignedUserId.Value,
                cancellationToken);

        return isMember ? Result.Success() : Result.Failure(TaskErrors.AssigneeNotAMember);
    }
}
