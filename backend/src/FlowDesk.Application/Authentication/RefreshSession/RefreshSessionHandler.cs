using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Authentication;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Authentication.RefreshSession;

/// <summary>
/// Exchanges a refresh token for a new token pair, and detects replay.
/// </summary>
/// <remarks>
/// Refresh tokens are single use. Presenting one that has already been
/// exchanged means the value exists in two places: the legitimate client still
/// holds it, or an attacker copied it. There is no way to tell which from the
/// request, so the safe reading is that the family is compromised — every token
/// in it is revoked and both parties are forced to sign in again.
///
/// This is the whole point of grouping tokens into families. Without it, a
/// stolen token could be rotated indefinitely alongside the real session and
/// nothing would ever look wrong.
/// </remarks>
public sealed class RefreshSessionHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly SessionIssuer _sessionIssuer;
    private readonly IClock _clock;

    public RefreshSessionHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ISecureTokenGenerator tokenGenerator,
        SessionIssuer sessionIssuer,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tokenGenerator = tokenGenerator;
        _sessionIssuer = sessionIssuer;
        _clock = clock;
    }

    public async Task<Result<AuthenticatedSession>> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshTokenValue))
        {
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.SessionNotFound);
        }

        // The lookup is by hash, so a database dump does not yield usable tokens.
        var presentedHash = _tokenGenerator.Hash(command.RefreshTokenValue);

        var storedToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == presentedHash, cancellationToken);

        if (storedToken is null)
        {
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.SessionNotFound);
        }

        var now = _clock.UtcNow;

        if (storedToken.UsedAt is not null)
        {
            await RevokeFamilyAsync(storedToken.FamilyId, now, cancellationToken);
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.SessionRevoked);
        }

        if (storedToken.RevokedAt is not null)
        {
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.SessionRevoked);
        }

        if (storedToken.IsExpired(now))
        {
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.SessionExpired);
        }

        var account = await _accountStore.FindByIdAsync(storedToken.UserId, cancellationToken);

        if (account is null)
        {
            // The account disappeared while the session was alive. Take the
            // session down with it rather than leaving a usable family behind.
            await RevokeFamilyAsync(storedToken.FamilyId, now, cancellationToken);
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.AccountNotFound);
        }

        var session = await _dbContext.ExecuteInTransactionAsync(
            async transactionCancellation =>
            {
                /*
                  Spending the token is a conditional update, not a read
                  followed by a write. The checks above ran on a copy read
                  without a lock, and two requests presenting the same token —
                  two tabs waking at once, or a thief racing the owner — both
                  pass them. Only one of them can move UsedAt from null; the
                  other waits on the row, finds it already spent and updates
                  nothing.

                  Without this, both requests went on to issue a successor, and
                  one single-use token became two independent sessions that
                  replay detection never sees (found in Phase 16).
                */
                var spent = await _dbContext.RefreshTokens
                    .Where(token => token.Id == storedToken.Id
                        && token.UsedAt == null
                        && token.RevokedAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(token => token.UsedAt, now),
                        transactionCancellation);

                if (spent == 0)
                {
                    return null;
                }

                // Keeps the tracked copy in step with the row, and the domain
                // rule in force for anything that reads it afterwards.
                storedToken.MarkUsed(now);

                var continued = _sessionIssuer.ContinueSession(account, storedToken.FamilyId);

                // Same transaction: the old token is spent and the new one
                // exists together, or neither change lands.
                await _dbContext.SaveChangesAsync(transactionCancellation);

                return continued;
            },
            cancellationToken);

        if (session is null)
        {
            /*
              Lost the race: the token was unused when this request read it and
              spent by the time it reached the row. That is a concurrent
              exchange, not a replay — two tabs waking together do exactly
              this — so the family is left alone and this request simply gets
              no session.

              Theft detection does not suffer. Whoever lost the race still holds
              only the spent token, and the next time they present it the read
              above finds it spent and revokes the family.
            */
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.SessionSuperseded);
        }

        return Result.Success(session);
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var familyTokens = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.Revoke(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
