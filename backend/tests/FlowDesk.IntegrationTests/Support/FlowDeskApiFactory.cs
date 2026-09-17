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
    private readonly string _brokerHost;
    private readonly int _brokerPort;

    /// <param name="brokerHost">
    /// Optional, and unreachable by default.
    /// </param>
    /// <param name="brokerPort">
    /// Port 1 by default, on purpose. A default of localhost:5672 would find
    /// the developer's own broker whenever one happened to be running, which
    /// makes a test depend on what else is on the machine. The connection is
    /// opened lazily, so an unreachable broker costs nothing until something
    /// publishes — and refuses immediately when it does.
    /// </param>
    public FlowDeskApiFactory(
        string connectionString,
        string brokerHost = "127.0.0.1",
        int brokerPort = 1)
    {
        _connectionString = connectionString;
        _brokerHost = brokerHost;
        _brokerPort = brokerPort;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = _connectionString,
                ["Postgres:HealthCheckTimeoutSeconds"] = "3",

                ["Messaging:Host"] = _brokerHost,
                ["Messaging:Port"] = _brokerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Messaging:VirtualHost"] = "/",
                ["Messaging:UserName"] = RabbitMqContainerFixture.UserName,
                ["Messaging:Password"] = RabbitMqContainerFixture.Password,
                // A per-run exchange, so two suites against one broker cannot
                // read each other's messages.
                ["Messaging:ExchangeName"] = $"flowdesk.tests.{Guid.CreateVersion7():N}",
                ["Messaging:PublishTimeoutSeconds"] = "5",

                /*
                  The API never sends mail — the worker does — but the options
                  are validated at startup, so the host needs values. Port 1 is
                  deliberate: if a test ever did send, it would fail loudly
                  rather than quietly reaching a mail server on this machine.
                */
                ["Email:Host"] = "127.0.0.1",
                ["Email:Port"] = "1",
                ["Email:UseStartTls"] = "false",
                ["Email:FromAddress"] = "tests@flowdesk.invalid",
                ["Email:FromDisplayName"] = "FlowDesk Tests",
                ["Email:WebBaseUrl"] = "http://localhost:3000",

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
