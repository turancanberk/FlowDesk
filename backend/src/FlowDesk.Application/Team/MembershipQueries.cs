using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team;

/// <summary>
/// Membership lookups shared by the team use cases.
/// </summary>
/// <remarks>
/// The last-owner question comes up in three places — listing the team,
/// changing a role and removing a member — and getting it wrong in one of them
/// would let a workspace end up with nobody who can manage it. Asking it in one
/// place keeps the three answers identical.
/// </remarks>
internal static class MembershipQueries
{
    public static Task<int> CountOwnersAsync(
        IFlowDeskDbContext dbContext,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships
            .AsNoTracking()
            .CountAsync(
                membership => membership.TenantId == tenantId
                    && membership.Role == MembershipRole.Owner,
                cancellationToken);

    /// <summary>
    /// The caller's role as it stands now, or null if they are no longer a
    /// member.
    /// </summary>
    /// <remarks>
    /// The role in the tenant context was read when the request began. For
    /// changes to the team itself that is too early: another owner may have
    /// demoted the caller in the meantime. Read under the team lock, this is
    /// the role the change is actually made with.
    /// </remarks>
    public static Task<MembershipRole?> CurrentRoleAsync(
        IFlowDeskDbContext dbContext,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.UserId == userId)
            .Select(membership => (MembershipRole?)membership.Role)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<bool> IsLastOwnerAsync(
        IFlowDeskDbContext dbContext,
        Membership membership,
        CancellationToken cancellationToken)
    {
        if (membership.Role is not MembershipRole.Owner)
        {
            return false;
        }

        return await CountOwnersAsync(dbContext, membership.TenantId, cancellationToken) <= 1;
    }
}
