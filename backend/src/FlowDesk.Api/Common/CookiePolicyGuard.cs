using FlowDesk.Api.Authentication;
using Microsoft.Extensions.Options;

namespace FlowDesk.Api.Common;

/// <summary>
/// Refuses to start in Production with a relaxed refresh-cookie policy.
/// </summary>
/// <remarks>
/// The <c>Secure</c> flag is configurable so a plain-HTTP local setup can work.
/// That switch would be an easy thing to carry into a deployment by accident,
/// and the result — a long-lived session credential sent over plaintext — is
/// exactly the failure the flag exists to prevent. Failing to start is louder
/// and cheaper than discovering it later.
/// </remarks>
public static class CookiePolicyGuard
{
    public static void EnsureSecureCookiePolicyInProduction(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsProduction())
        {
            return;
        }

        var policy = app.Services
            .GetRequiredService<IOptions<CookieOptionsSettings>>()
            .Value.SecurePolicy;

        if (policy is not CookieSecurityPolicy.Always)
        {
            throw new InvalidOperationException(
                "Auth:Cookies:SecurePolicy must be 'Always' in Production. " +
                "The refresh token cookie carries a long-lived credential and must not be sent over plain HTTP.");
        }
    }
}
