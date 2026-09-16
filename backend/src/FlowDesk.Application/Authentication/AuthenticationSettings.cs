namespace FlowDesk.Application.Authentication;

/// <summary>
/// Session lifetimes the application layer needs to know about.
/// </summary>
/// <remarks>
/// Deliberately separate from the infrastructure options that hold signing keys
/// and issuer names: the use cases care how long a session lasts, not how a
/// token is signed.
/// </remarks>
/// <param name="AccessTokenLifetime">
/// Kept short. Access tokens are not tracked server-side, so revoking a session
/// takes effect once the current access token expires (ADR-0006).
/// </param>
/// <param name="RefreshTokenLifetime">How long a session can be kept alive by rotation.</param>
public sealed record AuthenticationSettings(
    TimeSpan AccessTokenLifetime,
    TimeSpan RefreshTokenLifetime);
