using FlowDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Authentication.LogoutSession;

/// <summary>
/// Ends a session server-side.
/// </summary>
/// <remarks>
/// Revokes the whole family, not just the presented token: signing out should
/// end the session, and leaving sibling tokens alive would mean a copy taken
/// earlier still worked.
///
/// Always reports success. A sign-out request carrying an unknown or already
/// revoked token has achieved what the caller wanted, and returning an error
/// would both confuse the client and confirm to a prober whether a token value
/// exists.
/// </remarks>
public sealed class LogoutSessionHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IClock _clock;

    public LogoutSessionHandler(
        IFlowDeskDbContext dbContext,
        ISecureTokenGenerator tokenGenerator,
        IClock clock)
    {
        _dbContext = dbContext;
        _tokenGenerator = tokenGenerator;
        _clock = clock;
    }

    public async Task HandleAsync(LogoutSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshTokenValue))
        {
            return;
        }

        var presentedHash = _tokenGenerator.Hash(command.RefreshTokenValue);

        var familyId = await _dbContext.RefreshTokens
            .Where(token => token.TokenHash == presentedHash)
            .Select(token => (Guid?)token.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (familyId is null)
        {
            return;
        }

        var now = _clock.UtcNow;

        var familyTokens = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId.Value && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.Revoke(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
