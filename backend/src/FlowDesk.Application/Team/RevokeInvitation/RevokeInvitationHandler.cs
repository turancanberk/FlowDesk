using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.RevokeInvitation;

public sealed class RevokeInvitationHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public RevokeInvitationHandler(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.RevokeInvitations))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Daveti iptal etmek"));
        }

        var tenantId = _tenantContext.TenantId;

        var invitation = await _dbContext.Invitations
            .FirstOrDefaultAsync(
                candidate => candidate.Id == invitationId && candidate.TenantId == tenantId,
                cancellationToken);

        if (invitation is null)
        {
            return Result.Failure(TeamErrors.InvitationNotFound);
        }

        if (invitation.AcceptedAt is not null)
        {
            // Already used. Withdrawing it would achieve nothing; the person is
            // a member now and has to be removed as one.
            return Result.Failure(TeamErrors.InvitationNotFound);
        }

        invitation.Revoke(_clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
