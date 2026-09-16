namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Issues short-lived access tokens.
/// </summary>
/// <remarks>
/// Signing algorithm, key material and claim layout are infrastructure
/// details. Use cases only need "give me a token for this account and tell me
/// when it stops working".
/// </remarks>
public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount account);
}

/// <param name="Value">The encoded token, sent in the <c>Authorization</c> header.</param>
/// <param name="ExpiresAt">
/// When it stops being accepted. Returned to the client so it can refresh
/// ahead of expiry instead of discovering it through a failed request.
/// </param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
