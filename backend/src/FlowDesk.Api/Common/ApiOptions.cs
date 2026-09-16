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
