using FlowDesk.Application.Abstractions;

namespace FlowDesk.Application.Authentication;

/// <summary>
/// The pair of tokens produced by a successful sign-in or refresh.
/// </summary>
/// <param name="AccessToken">
/// Short-lived. Held only in browser memory and sent in the
/// <c>Authorization</c> header (ADR-0006).
/// </param>
/// <param name="RefreshTokenValue">
/// Long-lived. The API writes it into an <c>HttpOnly</c> cookie; it is never
/// returned in a response body and never stored in raw form on the server.
/// </param>
/// <param name="RefreshTokenExpiresAt">Drives the cookie lifetime.</param>
/// <param name="User">The signed-in account, for the client to display.</param>
public sealed record AuthenticatedSession(
    AccessToken AccessToken,
    string RefreshTokenValue,
    DateTimeOffset RefreshTokenExpiresAt,
    UserAccount User);
