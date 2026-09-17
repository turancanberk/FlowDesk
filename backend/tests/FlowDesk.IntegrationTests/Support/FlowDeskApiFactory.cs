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
    private readonly string _storageConnectionString;
    private readonly string _cacheConnectionString;

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
    /// <param name="storageConnectionString">
    /// Optional, and deliberately unreachable by default for the same reason as
    /// the broker: a test that never uploads should not depend on what else is
    /// running on the machine.
    /// </param>
    public FlowDeskApiFactory(
        string connectionString,
        string brokerHost = "127.0.0.1",
        int brokerPort = 1,
        string? storageConnectionString = null,
        string? cacheConnectionString = null)
    {
        _connectionString = connectionString;
        _brokerHost = brokerHost;
        _brokerPort = brokerPort;
        _storageConnectionString = storageConnectionString
            ?? "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
               + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
               + "BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;";

        /*
          Empty by default, which registers the null cache. A test that does not
          care about caching should behave as though there is none — not depend
          on whatever Redis happens to be running on the machine.
        */
        _cacheConnectionString = cacheConnectionString ?? string.Empty;
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

                ["Storage:ConnectionString"] = _storageConnectionString,
                // A container per test run, so one run's blobs cannot be seen
                // by another against a shared emulator.
                ["Storage:ContainerName"] = $"tests-{Guid.CreateVersion7():N}",
                ["Storage:MaximumFileSizeBytes"] = "1048576",

                ["Cache:ConnectionString"] = _cacheConnectionString,
                ["Cache:DashboardTtlSeconds"] = "60",

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
