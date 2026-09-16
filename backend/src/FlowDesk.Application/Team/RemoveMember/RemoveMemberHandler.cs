using FlowDesk.Application.Abstractions;
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
    private readonly ITenantContext _tenantContext;

    public RemoveMemberHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
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
            && _tenantContext.Role is not MembershipRole.Owner)
        {
            return Result.Failure(TeamErrors.CannotManageOwner);
        }

        if (await MembershipQueries.IsLastOwnerAsync(_dbContext, membership, cancellationToken))
        {
            // Applies to leaving as well: the last owner walking out would
            // leave the workspace unmanageable.
            return Result.Failure(TeamErrors.LastOwner);
        }

        _dbContext.Memberships.Remove(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
