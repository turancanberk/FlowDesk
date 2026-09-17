using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.InviteMember;

/// <summary>
/// Invites someone to the workspace.
/// </summary>
/// <remarks>
/// The raw token is returned once and never stored; only its hash is persisted,
/// so a database leak yields no usable invitations.
///
/// It is also queued for delivery by e-mail. The token still comes back in the
/// response: an admin who wants to hand the link over directly can, and a
/// mail server that is down should not stop them.
/// </remarks>
public sealed class InviteMemberHandler
{
    /// <summary>
    /// Long enough for someone to act on after a weekend, short enough that a
    /// forgotten link does not stay usable indefinitely.
    /// </summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IMessagePublisher _publisher;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public InviteMemberHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ISecureTokenGenerator tokenGenerator,
        IMessagePublisher publisher,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tokenGenerator = tokenGenerator;
        _publisher = publisher;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<CreatedInvitation>> HandleAsync(
        InviteMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.InviteMembers))
        {
            return Result.Failure<CreatedInvitation>(
                TenancyErrors.InsufficientRole("Ekibe davet göndermek"));
        }

        // Only an Owner can hand out ownership; otherwise an Admin could invite
        // an accomplice as Owner and escalate through them.
        if (command.Role is MembershipRole.Owner && _tenantContext.Role is not MembershipRole.Owner)
        {
            return Result.Failure<CreatedInvitation>(TeamErrors.CannotManageOwner);
        }

        var email = command.Email.Trim().ToLowerInvariant();
        var tenantId = _tenantContext.TenantId;
        var now = _clock.UtcNow;

        var existingAccount = await _accountStore.FindByEmailAsync(email, cancellationToken);

        if (existingAccount is not null)
        {
            var alreadyMember = await _dbContext.Memberships
                .AsNoTracking()
                .AnyAsync(
                    membership => membership.TenantId == tenantId
                        && membership.UserId == existingAccount.Id,
                    cancellationToken);

            if (alreadyMember)
            {
                return Result.Failure<CreatedInvitation>(TeamErrors.AlreadyAMember);
            }
        }

        var hasPendingInvitation = await _dbContext.Invitations
            .AsNoTracking()
            .AnyAsync(
                invitation => invitation.TenantId == tenantId
                    && invitation.Email == email
                    && invitation.AcceptedAt == null
                    && invitation.RevokedAt == null
                    && invitation.ExpiresAt > now,
                cancellationToken);

        if (hasPendingInvitation)
        {
            return Result.Failure<CreatedInvitation>(TeamErrors.InvitationAlreadyPending);
        }

        var secureToken = _tokenGenerator.Generate();

        var invitation = Invitation.Create(
            tenantId,
            email,
            command.Role,
            secureToken.Hash,
            _tenantContext.UserId,
            now,
            InvitationLifetime);

        _dbContext.Invitations.Add(invitation);

        // Queued before saving, so a rejected invitation never produces an
        // e-mail for an invitation that does not exist (ADR-0033).
        await _publisher.PublishAsync(
            new MemberInvited(
                Guid.CreateVersion7(),
                tenantId,
                now,
                invitation.Id,
                invitation.Email,
                invitation.Role,
                secureToken.RawValue,
                invitation.ExpiresAt,
                _tenantContext.UserId,
                _tenantContext.Slug),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreatedInvitation(
            new PendingInvitation(
                invitation.Id,
                invitation.Email,
                invitation.Role,
                invitation.CreatedAt,
                invitation.ExpiresAt),
            secureToken.RawValue));
    }
}
