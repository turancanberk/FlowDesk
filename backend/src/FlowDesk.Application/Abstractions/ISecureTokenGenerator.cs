namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Produces refresh tokens and hashes them for storage.
/// </summary>
/// <remarks>
/// Both halves live behind one contract because they must agree: a hash
/// computed one way and verified another is a silent authentication bypass.
/// Keeping generation and hashing together makes that pairing impossible to
/// get wrong from the outside.
/// </remarks>
public interface ISecureTokenGenerator
{
    /// <summary>Creates a new token together with the hash to persist.</summary>
    SecureToken Generate();

    /// <summary>Hashes a token presented by a client so it can be looked up.</summary>
    string Hash(string rawToken);
}

/// <param name="RawValue">Sent to the client once and never stored.</param>
/// <param name="Hash">What the database keeps.</param>
public sealed record SecureToken(string RawValue, string Hash);
