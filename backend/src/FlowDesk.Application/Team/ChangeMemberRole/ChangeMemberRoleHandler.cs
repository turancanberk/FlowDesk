using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.ChangeMemberRole;

/// <summary>
/// Changes a teammate's role.
/// </summary>
/// <remarks>
/// Two rules beyond the permission check:
///
/// An Admin cannot change an Owner's role. Admins may manage the team, but
/// letting them demote the people above them would make the Owner role
/// meaningless.
///
/// The last Owner cannot be demoted. A workspace with no Owner could not be
/// administered by anyone — the rule itself lives on the entity, and this
/// handler supplies the count it cannot see for itself.
/// </remarks>
public sealed class ChangeMemberRoleHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly ITenantContext _tenantContext;

    public ChangeMemberRoleHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext, IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _activity = activity;
    }

    public async Task<Result> HandleAsync(
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageMembers))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Üye rollerini değiştirmek"));
        }

        var tenantId = _tenantContext.TenantId;

        var membership = await _dbContext.Memberships
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.UserId == command.UserId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure(TeamErrors.MemberNotFound);
        }

        var callerIsOwner = _tenantContext.Role is MembershipRole.Owner;

        if (membership.Role is MembershipRole.Owner && !callerIsOwner)
        {
            return Result.Failure(TeamErrors.CannotManageOwner);
        }

        // Promoting someone to Owner is an owner-only act: an Admin who could
        // do it would effectively be able to grant themselves ownership through
        // an accomplice.
        if (command.Role is MembershipRole.Owner && !callerIsOwner)
        {
            return Result.Failure(TeamErrors.CannotManageOwner);
        }

        var isLastOwner = await MembershipQueries.IsLastOwnerAsync(
            _dbContext,
            membership,
            cancellationToken);

        var previousRole = membership.Role;

        try
        {
            membership.ChangeRole(command.Role, isLastOwner);
        }
        catch (DomainRuleViolationException)
        {
            return Result.Failure(TeamErrors.LastOwner);
        }

        // Only a real change. Re-saving the same role, which the interface
        // allows, is not a line in the history.
        if (membership.Role != previousRole)
        {
            _activity.Record(
                ActivityType.MemberRoleChanged,
                ActivitySubject.Member,
                membership.UserId,
                new MemberActivityPayload(membership.UserId, previousRole, membership.Role));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
