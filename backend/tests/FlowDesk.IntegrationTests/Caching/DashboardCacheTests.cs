using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Caching;

/// <summary>
/// The dashboard cache.
/// </summary>
/// <remarks>
/// Two things matter here and only two. One workspace's figures must never be
/// served to another — the single real risk in caching this data — and an
/// unreachable cache must change nothing a user sees. Speed is not asserted;
/// a test that measured it would fail on a loaded machine and prove nothing on
/// a fast one (ADR-0037).
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class DashboardCacheTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly RedisContainerFixture _redis;

    public DashboardCacheTests(PostgresContainerFixture postgres, RedisContainerFixture redis)
    {
        _postgres = postgres;
        _redis = redis;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private FlowDeskApiFactory CreateCachedFactory() =>
        new(_postgres.ConnectionString, cacheConnectionString: _redis.ConnectionString);

    /// <summary>
    /// A cached dashboard is served again, and says when it was taken.
    /// </summary>
    [Fact]
    public async Task A_second_read_comes_back_from_the_cache()
    {
        await using var factory = CreateCachedFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var first = await ReadAsync(workspace.Client, workspace.Slug);
        var second = await ReadAsync(workspace.Client, workspace.Slug);

        // The instant is part of the stored copy, so an identical timestamp is
        // what distinguishes a cache hit from a recomputation.
        Assert.Equal(first.GeneratedAt, second.GeneratedAt);
    }

    /// <summary>
    /// One workspace's figures are never served to another.
    /// </summary>
    /// <remarks>
    /// A security boundary, not a performance check (docs/SECURITY.md). The key
    /// carries the workspace; without it, whichever workspace read first would
    /// answer for every other.
    /// </remarks>
    [Fact]
    public async Task A_cached_dashboard_never_crosses_workspaces()
    {
        await using var factory = CreateCachedFactory();

        using var busy = await TestWorkspace.CreateAsync(factory, Cancellation);
        await TicketTestClient.CreateAndReadAsync(
            busy.Client, busy.Slug, busy.CustomerId, Cancellation);

        // Warms the cache for the busy workspace.
        var busyDashboard = await ReadAsync(busy.Client, busy.Slug);
        Assert.Equal(1, busyDashboard.OpenTicketCount);

        using var quiet = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var quietWorkspace = await WorkspaceTestClient.CreateOwnedAsync(quiet.Client, Cancellation);

        var quietDashboard = await ReadAsync(quiet.Client, quietWorkspace.Slug);

        Assert.Equal(0, quietDashboard.CustomerCount);
        Assert.Equal(0, quietDashboard.OpenTicketCount);
        Assert.Equal(1, quietDashboard.MemberCount);
    }

    /// <summary>
    /// A change in the workspace clears its cached figures.
    /// </summary>
    /// <remarks>
    /// Without this, someone who had just raised a ticket would watch the count
    /// stay wrong for as long as the entry lived — and would reasonably conclude
    /// the product had lost their work.
    /// </remarks>
    [Fact]
    public async Task Writing_something_clears_the_cached_figures()
    {
        await using var factory = CreateCachedFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var before = await ReadAsync(workspace.Client, workspace.Slug);
        Assert.Equal(0, before.OpenTicketCount);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        var after = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(1, after.OpenTicketCount);
        Assert.NotEqual(before.GeneratedAt, after.GeneratedAt);
    }

    /// <summary>
    /// Writing in one workspace does not clear another's.
    /// </summary>
    /// <remarks>
    /// The invalidation is as workspace-scoped as the key. Clearing everything
    /// on every write would work and would also make the cache pointless.
    /// </remarks>
    [Fact]
    public async Task Writing_in_one_workspace_leaves_anothers_cache_alone()
    {
        await using var factory = CreateCachedFactory();

        using var first = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var second = await TestWorkspace.CreateAsync(factory, Cancellation);

        var firstBefore = await ReadAsync(first.Client, first.Slug);

        await TicketTestClient.CreateAndReadAsync(
            second.Client, second.Slug, second.CustomerId, Cancellation);

        var firstAfter = await ReadAsync(first.Client, first.Slug);

        Assert.Equal(firstBefore.GeneratedAt, firstAfter.GeneratedAt);
    }

    /// <summary>
    /// An unreachable cache changes nothing a user sees.
    /// </summary>
    /// <remarks>
    /// This is what keeps the cache an optimisation rather than a dependency. A
    /// product that stops working because Redis is down has made a speed
    /// improvement into a single point of failure (ADR-0037).
    /// </remarks>
    [Fact]
    public async Task An_unreachable_cache_leaves_the_dashboard_working()
    {
        // Port 1: nothing is listening, and the client will not reach it.
        await using var factory = new FlowDeskApiFactory(
            _postgres.ConnectionString, cacheConnectionString: "127.0.0.1:1,connectTimeout=200");

        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        var dashboard = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(1, dashboard.OpenTicketCount);
        Assert.Equal(1, dashboard.CustomerCount);
    }

    /// <summary>
    /// With no cache configured at all, every read is fresh.
    /// </summary>
    /// <remarks>
    /// "No cache" is a supported configuration, not a broken one. Every other
    /// test in the suite runs this way, which is why the rest of them can assert
    /// on figures without thinking about staleness.
    /// </remarks>
    [Fact]
    public async Task With_no_cache_configured_every_read_is_fresh()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var first = await ReadAsync(workspace.Client, workspace.Slug);
        var second = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.NotEqual(first.GeneratedAt, second.GeneratedAt);
    }

    private static async Task<DashboardResponse> ReadAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync(
            new Uri($"/api/workspaces/{slug}/dashboard", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            FlowDeskJson.Options, Cancellation);

        Assert.NotNull(dashboard);

        return dashboard;
    }
}
