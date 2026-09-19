using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.RemoveMember;

/// <summary>
/// Removes someone from the workspace.
/// </summary>
/// <remarks>
/// Deleting the membership takes effect on the member's very next request,
/// because every request resolves the workspace through a membership lookup
/// rather than through a claim in their token (docs/SECURITY.md).
/// </remarks>
public sealed class RemoveMemberHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly ITenantContext _tenantContext;

    public RemoveMemberHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext, IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _activity = activity;
    }

    public async Task<Result> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isSelf = userId == _tenantContext.UserId;

        // Leaving is not the same act as removing someone else, and every
        // member may leave a workspace they joined.
        if (!isSelf && !WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageMembers))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Üye çıkarmak"));
        }

        var tenantId = _tenantContext.TenantId;

        return await _dbContext.ExecuteInTransactionAsync(
            async transactionCancellation =>
            {
                // Everything below reads the team, so it reads it under the
                // team lock: two owners leaving at once must not both see the
                // other still there (found in Phase 16).
                await _dbContext.LockTeamAsync(tenantId, transactionCancellation);

                return await RemoveAsync(tenantId, userId, isSelf, transactionCancellation);
            },
            cancellationToken);
    }

    private async Task<Result> RemoveAsync(
        Guid tenantId,
        Guid userId,
        bool isSelf,
        CancellationToken cancellationToken)
    {
        var callerRole = await MembershipQueries.CurrentRoleAsync(
            _dbContext, tenantId, _tenantContext.UserId, cancellationToken);

        if (callerRole is not { } role)
        {
            // Removed while this request was on its way in.
            return Result.Failure(TenancyErrors.WorkspaceNotFound);
        }

        // Checked again with the role as it stands now, not as it stood when
        // the request began.
        if (!isSelf && !WorkspacePermissions.IsGranted(role, WorkspaceAction.ManageMembers))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Üye çıkarmak"));
        }

        var membership = await _dbContext.Memberships
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.UserId == userId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure(TeamErrors.MemberNotFound);
        }

        if (!isSelf
            && membership.Role is MembershipRole.Owner
            && role is not MembershipRole.Owner)
        {
            return Result.Failure(TeamErrors.CannotManageOwner);
        }

        if (await MembershipQueries.IsLastOwnerAsync(_dbContext, membership, cancellationToken))
        {
            // Applies to leaving as well: the last owner walking out would
            // leave the workspace unmanageable.
            return Result.Failure(TeamErrors.LastOwner);
        }

        _activity.Record(
            ActivityType.MemberRemoved,
            ActivitySubject.Member,
            membership.UserId,
            new MemberActivityPayload(membership.UserId, membership.Role, membership.Role));

        _dbContext.Memberships.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
