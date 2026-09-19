using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FlowDesk.Api.Common;

/// <summary>
/// Rate limits for the endpoints worth attacking.
/// </summary>
/// <remarks>
/// Partitioned by client IP address. This is a blunt key — a shared office
/// address counts as one client — but it is the only identifier available
/// before the caller has authenticated, which is exactly when these endpoints
/// need protecting. Limits are set well above normal human use so a real user
/// retrying a password never meets them.
/// </remarks>
public static class RateLimitingPolicies
{
    /// <summary>Password guessing.</summary>
    public const string Login = "auth-login";

    /// <summary>Bulk account creation.</summary>
    public const string Registration = "auth-registration";

    /// <summary>Refresh token guessing and abuse.</summary>
    public const string Refresh = "auth-refresh";

    /// <summary>Invitation token guessing.</summary>
    public const string InvitationAcceptance = "invitation-accept";

    /// <summary>
    /// Registers the policies with limits read from <c>RateLimiting</c>, whose
    /// defaults are the production limits (<see cref="RateLimitingSettings"/>).
    /// </summary>
    public static IServiceCollection AddFlowDeskRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<RateLimitingSettings>()
            .Bind(configuration.GetSection(RateLimitingSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        // Read when the limiter is first built, after every configuration
        // source has been added — not at registration time.
        services
            .AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingSettings>>(
                (options, settings) => Configure(options, settings.Value));

        return services;
    }

    public static void Configure(RateLimiterOptions options, RateLimitingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            context.HttpContext.Response.ContentType = "application/problem+json";

            await context.HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.29",
                    title = "Çok fazla istek",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Çok fazla deneme yaptınız. Lütfen biraz bekleyip tekrar deneyin.",
                    code = "request.rate_limited",
                    traceId = context.HttpContext.TraceIdentifier,
                },
                cancellationToken);
        };

        AddFixedWindow(options, Login, settings.LoginPermitLimit, TimeSpan.FromMinutes(1));
        AddFixedWindow(options, Registration, settings.RegistrationPermitLimit, TimeSpan.FromMinutes(10));
        AddFixedWindow(options, Refresh, settings.RefreshPermitLimit, TimeSpan.FromMinutes(1));

        // An invitation token is 256 bits of randomness, so guessing is not a
        // realistic threat; the limit is there to stop the endpoint being used
        // as a probe, not to protect the token.
        AddFixedWindow(
            options, InvitationAcceptance, settings.InvitationAcceptancePermitLimit, TimeSpan.FromMinutes(10));
    }

    private static void AddFixedWindow(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window) =>
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    // No queueing: a caller past the limit is told so now
                    // rather than held open, which would tie up connections
                    // during exactly the burst we are defending against.
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                }));
}
