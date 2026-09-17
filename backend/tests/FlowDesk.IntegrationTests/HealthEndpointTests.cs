using System.Net;
using System.Text.Json;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests;

/// <summary>
/// Verifies that liveness and readiness answer different questions, and that
/// readiness genuinely reflects the state of its dependencies rather than
/// always reporting success.
/// </summary>
/// <remarks>
/// The two dependencies are deliberately not treated the same. PostgreSQL is
/// required: without it the API can answer nothing, so readiness fails and the
/// instance leaves rotation. The broker is not: every read and every write
/// still works, only the messages that would have followed are delayed. Making
/// the broker fail readiness would take every healthy instance out at once over
/// something none of them needs to serve a request.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class HealthEndpointTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly RabbitMqContainerFixture _broker;

    public HealthEndpointTests(PostgresContainerFixture postgres, RabbitMqContainerFixture broker)
    {
        _postgres = postgres;
        _broker = broker;
    }

    /// <summary>A factory pointed at both running containers.</summary>
    private FlowDeskApiFactory CreateHealthyFactory() =>
        new(_postgres.ConnectionString, _broker.Host, _broker.Port);

    [Fact]
    public async Task Liveness_returns_ok_when_the_process_is_running()
    {
        await using var factory = CreateHealthyFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_ok_when_the_dependencies_are_reachable()
    {
        await using var factory = CreateHealthyFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_names_each_dependency_and_its_status()
    {
        await using var factory = CreateHealthyFactory();
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());

        var postgresCheck = Assert.Single(checks, entry => entry.GetProperty("name").GetString() == "postgres");
        Assert.Equal("Healthy", postgresCheck.GetProperty("status").GetString());

        var brokerCheck = Assert.Single(checks, entry => entry.GetProperty("name").GetString() == "rabbitmq");
        Assert.Equal("Healthy", brokerCheck.GetProperty("status").GetString());
    }

    /// <summary>
    /// An unreachable broker degrades readiness without failing it.
    /// </summary>
    /// <remarks>
    /// This is the distinction the probe exists to make. The instance keeps
    /// serving, so it stays in rotation and the response still says 200; the
    /// body names the broker as degraded so an operator sees it before the
    /// backlog does.
    /// </remarks>
    [Fact]
    public async Task Readiness_degrades_but_stays_ok_when_the_broker_is_unreachable()
    {
        // Default broker host and port are deliberately unreachable.
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", document.RootElement.GetProperty("status").GetString());

        var postgresCheck = Assert.Single(checks, entry => entry.GetProperty("name").GetString() == "postgres");
        Assert.Equal("Healthy", postgresCheck.GetProperty("status").GetString());

        var brokerCheck = Assert.Single(checks, entry => entry.GetProperty("name").GetString() == "rabbitmq");
        Assert.Equal("Degraded", brokerCheck.GetProperty("status").GetString());
    }

    /// <summary>
    /// The point of a readiness probe is that it can fail. Pointing the host at
    /// a port nothing is listening on proves the probe reports the dependency
    /// rather than reporting its own liveness a second time.
    /// </summary>
    /// <remarks>
    /// Unlike the broker, the database is required: a 503 here is correct,
    /// because an instance that cannot reach it cannot answer anything.
    /// </remarks>
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
