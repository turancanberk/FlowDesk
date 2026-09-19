using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.AcceptInvitation;

/// <summary>
/// Redeems an invitation and joins the caller to the workspace.
/// </summary>
/// <remarks>
/// Runs outside a workspace scope on purpose: the person accepting is not a
/// member yet, so the usual membership check would reject them before they
/// could join. The scope comes from the token itself, which is why the token is
/// treated as a credential (docs/API_CONVENTIONS.md).
///
/// Every failure returns the same error. Telling a caller that a token exists
/// but has expired, or that it belongs to a different address, hands them
/// information they did not have; someone holding a valid link never sees it.
/// </remarks>
public sealed class AcceptInvitationHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly IUserAccountStore _accountStore;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AcceptInvitationHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ISecureTokenGenerator tokenGenerator,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tokenGenerator = tokenGenerator;
        _currentUser = currentUser;
        _clock = clock;
        _activity = activity;
    }

    public async Task<Result<AcceptedInvitation>> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure<AcceptedInvitation>(TeamErrors.InvitationNotUsable);
        }

        var account = await _accountStore.FindByIdAsync(_currentUser.Id, cancellationToken);

        if (account is null)
        {
            return Result.Failure<AcceptedInvitation>(TeamErrors.InvitationNotUsable);
        }

        // Looked up by hash, so a database dump yields no redeemable links.
        var tokenHash = _tokenGenerator.Hash(command.Token);

        // IgnoreQueryFilters: the invitation belongs to a workspace the caller
        // is not yet a member of, so no workspace is in scope and the tenant
        // filter would hide the very row being redeemed. Safe here because the
        // token is what proves the caller may see it.
        var invitation = await _dbContext.Invitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<AcceptedInvitation>(TeamErrors.InvitationNotUsable);
        }

        var now = _clock.UtcNow;

        try
        {
            invitation.Accept(account.Email, now);
        }
        catch (DomainRuleViolationException)
        {
            // Expired, already used, withdrawn, or addressed to someone else —
            // all one answer.
            return Result.Failure<AcceptedInvitation>(TeamErrors.InvitationNotUsable);
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == invitation.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<AcceptedInvitation>(TeamErrors.InvitationNotUsable);
        }

        var accepted = await _dbContext.ExecuteInTransactionAsync(
            async transactionCancellation =>
            {
                /*
                  Spending the invitation is a conditional update, for the same
                  reason as spending a refresh token. The checks above ran on a
                  copy read without a lock, and a double-clicked link sends two
                  requests that both pass them. Only one can move AcceptedAt
                  from null; the other waits on the row, finds it spent and goes
                  no further.

                  Without this, both went on to add a membership and the second
                  failed on the unique index with a server error (found in
                  Phase 16).
                */
                var spent = await _dbContext.Invitations
                    .IgnoreQueryFilters()
                    .Where(candidate => candidate.Id == invitation.Id
                        && candidate.AcceptedAt == null
                        && candidate.RevokedAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(candidate => candidate.AcceptedAt, now),
                        transactionCancellation);

                if (spent == 0)
                {
                    return false;
                }

                var alreadyMember = await _dbContext.Memberships
                    .AsNoTracking()
                    .AnyAsync(
                        membership => membership.TenantId == invitation.TenantId
                            && membership.UserId == account.Id,
                        transactionCancellation);

                if (!alreadyMember)
                {
                    _dbContext.Memberships.Add(Membership.Create(
                        account.Id,
                        invitation.TenantId,
                        invitation.Role,
                        now));
                }

                /*
                  The one place a workspace is named explicitly. The person is
                  not a member until this moment, so there is no resolved
                  workspace to read — the invitation says which one (ADR-0036).
                */
                _activity.RecordOutsideWorkspace(
                    invitation.TenantId,
                    account.Id,
                    ActivityType.MemberJoined,
                    ActivitySubject.Member,
                    account.Id,
                    new MemberActivityPayload(account.Id, null, invitation.Role));

                // Same transaction: the invitation is spent and the membership
                // exists together, or neither change lands. Otherwise a failure
                // between them could burn the invitation without granting access.
                await _dbContext.SaveChangesAsync(transactionCancellation);

                return true;
            },
            cancellationToken);

        if (!accepted)
        {
            // Someone else spent it first — the same answer as any spent link.
            return Result.Failure<AcceptedInvitation>(TeamErrors.InvitationNotUsable);
        }

        return Result.Success(new AcceptedInvitation(tenant.Id, tenant.Slug, tenant.Name));
    }
}
