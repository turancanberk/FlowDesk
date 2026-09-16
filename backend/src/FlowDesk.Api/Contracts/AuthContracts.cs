namespace FlowDesk.Api.Contracts;

/*
  HTTP response contracts for authentication.

  Requests bind directly to the application commands. Those commands are
  already purpose-built input models carrying only what a client may set, so a
  second identical record in this layer would be duplication that drifts: a rule
  added to one shape and forgotten on the other. The protection that matters —
  never binding to an EF Core entity — is intact either way
  (docs/API_CONVENTIONS.md).

  Responses do get their own types, because they genuinely differ from anything
  the application layer produces. The most important difference is below.
*/

/// <summary>
/// What a successful sign-in returns.
/// </summary>
/// <remarks>
/// The refresh token is <em>not</em> here. It leaves the server only in an
/// <c>HttpOnly</c> cookie; putting it in a body would make it readable by
/// JavaScript and undo the reason for the cookie (ADR-0006).
///
/// <c>accessTokenExpiresAt</c> lets the client refresh just before expiry
/// rather than discovering it through a failed request.
/// </remarks>
public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    CurrentUserResponse User);

public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName);
