using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Authentication;

/// <summary>
/// Token settings bound from the <c>Auth</c> configuration section.
/// </summary>
/// <remarks>
/// The signing key is supplied through environment variables, .NET user-secrets
/// or GitHub Secrets. It is never committed, and a build without it fails at
/// startup rather than issuing tokens anyone can forge.
/// </remarks>
public sealed class AuthenticationOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// HMAC signing key. At least 32 bytes, because HMAC-SHA256 gains nothing
    /// from a key shorter than its output and a short key is guessable.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "JWT signing key is not configured.")]
    [MinLength(32, ErrorMessage = "JWT signing key must be at least 32 characters.")]
    public string SigningKey { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = "flowdesk-api";

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = "flowdesk-web";

    /// <summary>
    /// Kept short: access tokens are not tracked server-side, so a revoked
    /// session stays usable until the current token expires (ADR-0006).
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenLifetimeMinutes { get; init; } = 10;

    [Range(1, 90)]
    public int RefreshTokenLifetimeDays { get; init; } = 14;
}
