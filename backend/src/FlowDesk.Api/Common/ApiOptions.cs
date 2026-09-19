using System.ComponentModel.DataAnnotations;
using FlowDesk.Api.Authentication;

namespace FlowDesk.Api.Common;

/// <summary>Cookie behaviour, bound from <c>Auth:Cookies</c>.</summary>
public sealed class CookieOptionsSettings
{
    public const string SectionName = "Auth:Cookies";

    public CookieSecurityPolicy SecurePolicy { get; init; } = CookieSecurityPolicy.Always;
}

/// <summary>Allowed browser origins, bound from <c>Cors</c>.</summary>
/// <remarks>
/// In production the frontend and the API are served from one origin behind
/// Caddy, so this list is empty and no CORS policy applies. It exists for local
/// development, where the two run on different ports.
/// </remarks>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    [Required]
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];
}

/// <summary>Per-address request limits, bound from <c>RateLimiting</c>.</summary>
/// <remarks>
/// The defaults are the production limits and nothing in the deployment
/// overrides them. They are configurable for one reason: the browser tests
/// create a handful of accounts on every run, all from one address, and a
/// limit that stops a bulk sign-up also stops the second run of the suite.
/// Lowering a limit is always allowed; the ranges only rule out a value that
/// would switch protection off by accident.
/// </remarks>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Sign-in attempts per minute.</summary>
    [Range(1, 1000)]
    public int LoginPermitLimit { get; init; } = 10;

    /// <summary>Accounts created per ten minutes.</summary>
    [Range(1, 1000)]
    public int RegistrationPermitLimit { get; init; } = 5;

    /// <summary>Session refreshes per minute.</summary>
    [Range(1, 1000)]
    public int RefreshPermitLimit { get; init; } = 30;

    /// <summary>Invitation acceptances per ten minutes.</summary>
    [Range(1, 1000)]
    public int InvitationAcceptancePermitLimit { get; init; } = 20;
}
