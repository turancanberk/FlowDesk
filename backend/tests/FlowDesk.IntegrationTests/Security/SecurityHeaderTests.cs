using System.Net;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Security;

/// <summary>
/// The headers every response carries (docs/SECURITY.md §14).
/// </summary>
/// <remarks>
/// Checked on the answers that are easiest to forget: the ones no endpoint
/// produced. A 404 from the router, a 401 from authentication and a 429 from
/// the rate limiter all leave the pipeline by different doors.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class SecurityHeaderTests
{
    private static readonly (string Name, string Value)[] Expected =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("Referrer-Policy", "no-referrer"),
        ("X-Frame-Options", "DENY"),
    ];

    private readonly PostgresContainerFixture _postgres;

    public SecurityHeaderTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Her_yanit_guvenlik_basliklarini_tasir()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var signedIn = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        // A success, a route that does not exist, and a request without a
        // session: three different ways out of the pipeline.
        using var ok = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Cancellation);
        using var missing = await client.GetAsync(new Uri("/api/yok", UriKind.Relative), Cancellation);
        using var unauthorised = await client.GetAsync(
            new Uri("/api/workspaces", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorised.StatusCode);

        foreach (var response in new[] { ok, missing, unauthorised })
        {
            foreach (var (name, value) in Expected)
            {
                Assert.True(
                    response.Headers.TryGetValues(name, out var actual)
                    || response.Content.Headers.TryGetValues(name, out actual),
                    $"{response.RequestMessage?.RequestUri} yanıtında {name} yok.");

                Assert.Equal(value, string.Join(",", actual!));
            }

            /*
              Contains rather than equals: the health check middleware adds its
              own "no-cache" beside ours. What matters is that nothing the API
              returns may be stored.
            */
            var cacheControl = string.Join(",", response.Headers.GetValues("Cache-Control"));

            Assert.Contains("no-store", cacheControl, StringComparison.Ordinal);

            var policy = string.Join(",", response.Headers.GetValues("Content-Security-Policy"));

            Assert.Contains("default-src 'none'", policy, StringComparison.Ordinal);
            Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        }

        // The server's identity is not advertised.
        Assert.False(ok.Headers.Contains("Server"));
    }

    /// <summary>
    /// A throttled answer is still an answer, and carries the same headers.
    /// </summary>
    [Fact]
    public async Task Rate_limit_yaniti_da_basliklari_tasir()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        HttpResponseMessage? throttled = null;

        try
        {
            for (var attempt = 0; attempt < 12 && throttled is null; attempt++)
            {
                var response = await AuthTestClient.LoginAsync(
                    client, "yok@ornek.test", "yanlis-parola", Cancellation);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throttled = response;
                }
                else
                {
                    response.Dispose();
                }
            }

            Assert.NotNull(throttled);

            Assert.Equal("nosniff", string.Join(",", throttled.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal("DENY", string.Join(",", throttled.Headers.GetValues("X-Frame-Options")));
        }
        finally
        {
            throttled?.Dispose();
        }
    }

    /// <summary>
    /// HSTS belongs to deployments that speak https, not to localhost.
    /// </summary>
    /// <remarks>
    /// A max-age served over plain http is ignored by browsers, but a stray
    /// one from a localhost port would pin every other project on the machine
    /// to https for as long as it lasted.
    /// </remarks>
    [Fact]
    public async Task Gelistirmede_HSTS_gonderilmez()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative), Cancellation);

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
