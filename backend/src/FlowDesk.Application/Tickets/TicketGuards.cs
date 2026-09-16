using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets;

/// <summary>
/// Checks that the records a ticket points at belong to the same workspace.
/// </summary>
/// <remarks>
/// This is the third isolation layer: the query filter scopes what a request
/// can read, but nothing stops a client from sending another workspace's
/// customer id in a request body. Verifying ownership before attaching is what
/// keeps a foreign record from being pulled into this workspace's data
/// (docs/SECURITY.md).
///
/// Shared because create, update and re-assign all need the same check, and one
/// of the three forgetting it would be the hole.
/// </remarks>
internal static class TicketGuards
{
    /// <summary>
    /// Confirms the customer exists in the current workspace.
    /// </summary>
    /// <remarks>
    /// Archived customers are accepted: an existing ticket must remain editable
    /// after its customer has been taken out of circulation, and refusing would
    /// strand the conversation.
    /// </remarks>
    public static async Task<Result> EnsureCustomerBelongsToWorkspaceAsync(
        IFlowDeskDbContext dbContext,
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Customers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                customer => customer.Id == customerId && customer.TenantId == tenantId,
                cancellationToken);

        return exists ? Result.Success() : Result.Failure(TicketErrors.CustomerNotFound);
    }

    /// <summary>
    /// Confirms the assignee is a member of the current workspace.
    /// </summary>
    /// <remarks>
    /// Without this, a ticket could be assigned to any user id in the system —
    /// including someone from another organisation, who would then appear on a
    /// ticket they cannot see.
    /// </remarks>
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

        return isMember ? Result.Success() : Result.Failure(TicketErrors.AssigneeNotAMember);
    }
}
