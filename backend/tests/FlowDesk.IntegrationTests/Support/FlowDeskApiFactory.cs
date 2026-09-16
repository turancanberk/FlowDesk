using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Boots the real API host in-process, pointed at a caller-supplied PostgreSQL
/// connection string.
/// </summary>
/// <remarks>
/// The application under test is the real one: the same DI registrations, the
/// same middleware pipeline and the same endpoints that run in production. Only
/// the database location and the signing key are substituted.
/// </remarks>
public sealed class FlowDeskApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// A signing key that exists only inside the test process. It is not a
    /// secret and is not the key any deployment uses; the application refuses
    /// to start without one, so the tests must supply their own.
    /// </summary>
    private const string TestSigningKey = "integration-tests-signing-key-not-a-real-secret-0123456789";

    private readonly string _connectionString;

    public FlowDeskApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = _connectionString,
                ["Postgres:HealthCheckTimeoutSeconds"] = "3",

                ["Auth:SigningKey"] = TestSigningKey,
                ["Auth:Issuer"] = "flowdesk-api-tests",
                ["Auth:Audience"] = "flowdesk-web-tests",
                ["Auth:AccessTokenLifetimeMinutes"] = "10",
                ["Auth:RefreshTokenLifetimeDays"] = "14",

                // The test client talks plain HTTP, and a Secure cookie would
                // never be stored by the handler. The flag is exercised by the
                // production guard instead (CookiePolicyGuard).
                ["Auth:Cookies:SecurePolicy"] = "SameAsRequest",

                // Requests from the test client are same-origin, so no CORS
                // policy is needed.
                ["Cors:AllowedOrigins:0"] = "",
            });
        });
    }

    /// <summary>
    /// Creates a client that keeps cookies, so refresh-token rotation can be
    /// exercised the way a browser would.
    /// </summary>
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
}
