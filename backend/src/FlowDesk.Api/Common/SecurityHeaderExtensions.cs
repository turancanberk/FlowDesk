namespace FlowDesk.Api.Common;

/// <summary>
/// The headers every API response carries (docs/SECURITY.md §14).
/// </summary>
/// <remarks>
/// Set by the application, not by the reverse proxy in front of it. Caddy will
/// serve the frontend and the API from one origin in production, and could set
/// these itself — but then local development, the integration tests and the
/// browser tests would all run without them, and the one deployment that
/// forgot a proxy rule would run without them too. Headers the application
/// owns travel with it.
///
/// The API answers JSON, not documents: nothing here should be framed,
/// embedded, or treated as anything but what its content type says.
/// </remarks>
public static class SecurityHeaderExtensions
{
    /*
      A policy for something that is never a document. default-src 'none'
      means a response that somehow rendered would be allowed to load nothing
      at all; frame-ancestors and form-action close the two things an attacker
      would otherwise try with an API response.
    */
    private const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    // Features the API has no use for. Written out rather than left to the
    // browser's defaults, which differ and change.
    private const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), "
        + "microphone=(), payment=(), usb=()";

    public static WebApplication UseFlowDeskSecurityHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            // No referrer at all: every API address carries a workspace slug,
            // and some carry record ids.
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            headers["Content-Security-Policy"] = ApiContentSecurityPolicy;
            headers["Permissions-Policy"] = PermissionsPolicy;

            /*
              Nothing the API returns may be stored: every response is either
              tenant data or a session, and a shared cache holding either is
              the kind of leak that survives sign-out.
            */
            headers["Cache-Control"] = "no-store";

            await next();
        });

        return app;
    }
}
