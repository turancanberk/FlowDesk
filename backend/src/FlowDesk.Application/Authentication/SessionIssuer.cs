using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Authentication;

namespace FlowDesk.Application.Authentication;

/// <summary>
/// Issues the access/refresh token pair for a session.
/// </summary>
/// <remarks>
/// Sign-in, registration and refresh all need to mint the same pair with the
/// same lifetimes. Keeping that in one place means a change to how sessions are
/// issued cannot be applied to two of the three paths and forgotten on the
/// third.
///
/// The persisted entity is added to the context but not saved here: the caller
/// decides the transaction boundary, and on refresh the old token must be
/// marked used in the same save as the new one is written.
/// </remarks>
public sealed class SessionIssuer
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IClock _clock;
    private readonly TimeSpan _refreshTokenLifetime;

    public SessionIssuer(
        IFlowDeskDbContext dbContext,
        IAccessTokenIssuer accessTokenIssuer,
        ISecureTokenGenerator tokenGenerator,
        IClock clock,
        AuthenticationSettings settings)
    {
        _dbContext = dbContext;
        _accessTokenIssuer = accessTokenIssuer;
        _tokenGenerator = tokenGenerator;
        _clock = clock;
        _refreshTokenLifetime = settings.RefreshTokenLifetime;
    }

    /// <summary>Starts a brand-new session family. Used on sign-in and registration.</summary>
    public AuthenticatedSession StartSession(UserAccount account)
    {
        var secureToken = _tokenGenerator.Generate();
        var now = _clock.UtcNow;

        var refreshToken = RefreshToken.StartFamily(
            account.Id,
            secureToken.Hash,
            now,
            _refreshTokenLifetime);

        _dbContext.RefreshTokens.Add(refreshToken);

        return Build(account, secureToken.RawValue, refreshToken.ExpiresAt);
    }

    /// <summary>Rotates within an existing family. Used on refresh.</summary>
    public AuthenticatedSession ContinueSession(UserAccount account, Guid familyId)
    {
        var secureToken = _tokenGenerator.Generate();
        var now = _clock.UtcNow;

        var refreshToken = RefreshToken.ContinueFamily(
            account.Id,
            familyId,
            secureToken.Hash,
            now,
            _refreshTokenLifetime);

        _dbContext.RefreshTokens.Add(refreshToken);

        return Build(account, secureToken.RawValue, refreshToken.ExpiresAt);
    }

    private AuthenticatedSession Build(UserAccount account, string rawToken, DateTimeOffset expiresAt) =>
        new(_accessTokenIssuer.Issue(account), rawToken, expiresAt, account);
}
