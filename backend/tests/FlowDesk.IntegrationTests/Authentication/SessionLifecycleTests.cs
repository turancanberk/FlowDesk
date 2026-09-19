using System.Net;
using System.Net.Http.Headers;
using FlowDesk.Application.Abstractions;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowDesk.IntegrationTests.Authentication;

/// <summary>
/// A session from sign-in to expiry and back, the way the web client lives it.
/// </summary>
/// <remarks>
/// The rotation tests cover the refresh endpoint on its own. These cover the
/// contract the web client's silent refresh is built on: an expired access
/// token on an ordinary request answers 401 — not 403, not 500 — and the token
/// that refresh then hands back works on the very next request.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class SessionLifecycleTests
{
    private readonly PostgresContainerFixture _postgres;

    public SessionLifecycleTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_expired_access_token_is_refused_and_a_refresh_restores_the_session()
    {
        var clock = new AdjustableClock();

        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        await using var host = WithClock(factory, clock);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // Signed in eleven minutes ago; the access token lives for ten.
        clock.Offset = TimeSpan.FromMinutes(-11);

        using var registration = await AuthTestClient.RegisterAsync(
            client, AuthTestClient.UniqueEmail(), Cancellation);
        var stale = await AuthTestClient.ReadSessionAsync(registration, Cancellation);

        clock.Offset = TimeSpan.Zero;

        using var refused = await GetWorkspacesAsync(client, stale.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // The web client keys its refresh on this challenge.
        var challenge = Assert.Single(refused.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Contains("invalid_token", challenge.Parameter, StringComparison.Ordinal);

        using var refreshed = await AuthTestClient.RefreshAsync(client, Cancellation);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var renewed = await AuthTestClient.ReadSessionAsync(refreshed, Cancellation);

        Assert.NotEqual(stale.AccessToken, renewed.AccessToken);
        Assert.Equal(stale.User.Id, renewed.User.Id);
        Assert.True(renewed.AccessTokenExpiresAt > DateTimeOffset.UtcNow);

        using var retried = await GetWorkspacesAsync(client, renewed.AccessToken);

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    /// <summary>
    /// Two refreshes racing with one token end with at most one live session.
    /// </summary>
    /// <remarks>
    /// Two browser tabs waking at once do exactly this. Refresh tokens are
    /// single use, so if both requests read the token before either marks it
    /// spent, one token becomes two independent sessions — and replay
    /// detection, which only looks for a spent token being presented again,
    /// never sees it.
    /// </remarks>
    [Fact]
    public async Task Simultaneous_refreshes_with_one_token_never_yield_two_sessions()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var registration = await AuthTestClient.RegisterAsync(
            client, AuthTestClient.UniqueEmail(), Cancellation);
        var session = await AuthTestClient.ReadSessionAsync(registration, Cancellation);
        var token = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(token);

        const int contenders = 8;

        // Separate clients without cookie handling, so each request carries the
        // same token and none of them picks up a rotated one.
        var clients = Enumerable.Range(0, contenders).Select(_ => factory.CreateClient()).ToList();

        try
        {
            using var start = new SemaphoreSlim(0, contenders);

            var attempts = clients.Select(async racer =>
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, new Uri("/api/auth/refresh", UriKind.Relative));
                request.Headers.Add("Cookie", $"flowdesk_refresh_token={token}");

                await start.WaitAsync(Cancellation);

                using var response = await racer.SendAsync(request, Cancellation);

                return response.StatusCode;
            }).ToList();

            start.Release(contenders);

            var outcomes = await Task.WhenAll(attempts);

            Assert.Equal(1, outcomes.Count(status => status == HttpStatusCode.OK));
            Assert.All(
                outcomes.Where(status => status != HttpStatusCode.OK),
                status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        }
        finally
        {
            foreach (var racer in clients)
            {
                racer.Dispose();
            }
        }

        await using var dbContext = _postgres.CreateDbContext();

        // The original plus exactly one successor. More rows would mean the
        // single-use token was exchanged more than once.
        var issued = await dbContext.RefreshTokens.CountAsync(
            refreshToken => refreshToken.UserId == session.User.Id, Cancellation);

        Assert.True(issued == 2, $"Tek kullanımlık token {issued - 1} kez takas edildi.");
    }

    private static WebApplicationFactory<Program> WithClock(FlowDeskApiFactory factory, IClock clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton(clock);
        }));

    private static async Task<HttpResponseMessage> GetWorkspacesAsync(HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/workspaces", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await client.SendAsync(request, Cancellation);
    }
}
