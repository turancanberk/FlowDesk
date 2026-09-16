using System.Net;
using System.Text.Json;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests;

/// <summary>
/// Verifies that liveness and readiness answer different questions, and that
/// readiness genuinely reflects the state of the database rather than always
/// reporting success.
/// </summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class HealthEndpointTests
{
    private readonly PostgresContainerFixture _postgres;

    public HealthEndpointTests(PostgresContainerFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Liveness_returns_ok_when_the_process_is_running()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_ok_when_the_database_is_reachable()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_names_the_database_check_and_its_status()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        var postgresCheck = Assert.Single(checks, entry => entry.GetProperty("name").GetString() == "postgres");
        Assert.Equal("Healthy", postgresCheck.GetProperty("status").GetString());
    }

    /// <summary>
    /// The point of a readiness probe is that it can fail. Pointing the host at
    /// a port nothing is listening on proves the probe reports the dependency
    /// rather than reporting its own liveness a second time.
    /// </summary>
    [Fact]
    public async Task Readiness_reports_unavailable_when_the_database_is_unreachable()
    {
        const string unreachableDatabase =
            "Host=127.0.0.1;Port=1;Database=flowdesk;Username=flowdesk;Password=irrelevant;Timeout=2";

        await using var factory = new FlowDeskApiFactory(unreachableDatabase);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// Liveness must not depend on the database: a failing dependency should
    /// take an instance out of rotation, not restart it in a loop.
    /// </summary>
    [Fact]
    public async Task Liveness_stays_ok_when_the_database_is_unreachable()
    {
        const string unreachableDatabase =
            "Host=127.0.0.1;Port=1;Database=flowdesk;Username=flowdesk;Password=irrelevant;Timeout=2";

        await using var factory = new FlowDeskApiFactory(unreachableDatabase);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
