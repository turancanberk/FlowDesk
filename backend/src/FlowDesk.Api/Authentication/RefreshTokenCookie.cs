using FlowDesk.Api.Common;

namespace FlowDesk.Api.Authentication;

/// <summary>
/// Reads and writes the refresh token cookie.
/// </summary>
/// <remarks>
/// The cookie carries the only long-lived credential in the system, so its
/// flags are set in exactly one place rather than at each call site where one
/// could quietly be forgotten.
///
/// <list type="bullet">
/// <item><c>HttpOnly</c> keeps it out of reach of JavaScript, so an XSS flaw
/// cannot read it.</item>
/// <item><c>Secure</c> keeps it off plaintext connections.</item>
/// <item><c>SameSite=Strict</c> means a cross-site request never carries it,
/// which is the primary CSRF defence for the two endpoints that accept
/// it.</item>
/// <item>A narrow <c>Path</c> keeps it off every other request, so it is not
/// sent across the wire dozens of times per page.</item>
/// </list>
/// </remarks>
public static class RefreshTokenCookie
{
    public const string Name = "flowdesk_refresh_token";

    /// <summary>
    /// Scoped to the auth endpoints. The browser then omits the cookie
    /// everywhere else, including the API calls that carry the bearer token.
    /// </summary>
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    public static void Write(
        HttpResponse response,
        string token,
        DateTimeOffset expiresAt,
        CookieSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, token, BuildOptions(policy, expiresAt));
    }

    public static void Clear(HttpResponse response, CookieSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Delete(Name, BuildOptions(policy, expires: null));
    }

    private static CookieOptions BuildOptions(CookieSecurityPolicy policy, DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            Secure = policy is CookieSecurityPolicy.Always,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = expires,
            IsEssential = true,
        };
}

/// <summary>
/// Whether the refresh cookie is restricted to HTTPS.
/// </summary>
/// <remarks>
/// Configurable only so that a plain-HTTP local setup can work. Production
/// leaves it at <see cref="Always"/>, and the API refuses to start in the
/// Production environment if it is relaxed.
/// </remarks>
public enum CookieSecurityPolicy
{
    Always,
    SameAsRequest,
}
