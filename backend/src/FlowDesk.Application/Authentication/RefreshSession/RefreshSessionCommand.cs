namespace FlowDesk.Application.Authentication.RefreshSession;

/// <param name="RefreshTokenValue">
/// The raw token taken from the <c>HttpOnly</c> cookie. Never read from a
/// request body or query string.
/// </param>
public sealed record RefreshSessionCommand(string RefreshTokenValue);
