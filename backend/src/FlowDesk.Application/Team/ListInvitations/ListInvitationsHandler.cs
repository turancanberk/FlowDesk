using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.ListInvitations;

/// <summary>
/// Invitations still waiting to be accepted.
/// </summary>
/// <remarks>
/// Accepted, withdrawn and expired invitations are left out: they are not
/// actionable, and showing them would turn a short list into a log.
/// </remarks>
public sealed class ListInvitationsHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ListInvitationsHandler(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<PendingInvitation>>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewInvitations))
        {
            return Result.Failure<IReadOnlyList<PendingInvitation>>(
                TenancyErrors.InsufficientRole("Davetleri görüntülemek"));
        }

        var tenantId = _tenantContext.TenantId;
        var now = _clock.UtcNow;

        var invitations = await _dbContext.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null
                && invitation.ExpiresAt > now)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Select(invitation => new PendingInvitation(
                invitation.Id,
                invitation.Email,
                invitation.Role,
                invitation.CreatedAt,
                invitation.ExpiresAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PendingInvitation>>(invitations);
    }
}
