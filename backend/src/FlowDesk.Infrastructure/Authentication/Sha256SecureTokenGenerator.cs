using System.Security.Cryptography;
using System.Text;
using FlowDesk.Application.Abstractions;

namespace FlowDesk.Infrastructure.Authentication;

/// <summary>
/// Generates refresh tokens and hashes them for storage.
/// </summary>
/// <remarks>
/// SHA-256 without a salt or work factor is the right choice here, and only
/// here. A refresh token is 256 bits of output from a cryptographic random
/// generator, so there is no dictionary to attack and nothing for a slow hash
/// to protect against; what we need is a fast, deterministic lookup key. User
/// passwords are a different problem and are handled by ASP.NET Core Identity's
/// PBKDF2 hasher.
/// </remarks>
public sealed class Sha256SecureTokenGenerator : ISecureTokenGenerator
{
    private const int TokenByteLength = 32;

    public SecureToken Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);

        // Base64Url: safe in a cookie and in a URL without escaping.
        var rawValue = Base64UrlEncode(bytes);

        return new SecureToken(rawValue, Hash(rawValue));
    }

    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        return Convert.ToHexStringLower(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
