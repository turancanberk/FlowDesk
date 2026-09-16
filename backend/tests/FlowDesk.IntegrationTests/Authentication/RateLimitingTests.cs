using System.Net;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Authentication;

/// <summary>
/// The rate limits protecting the endpoints worth attacking
/// (docs/SECURITY.md).
/// </summary>
/// <remarks>
/// Each test class elsewhere builds its own host, so limiter counters start
/// fresh and the limits never come into play. That makes them invisible to the
/// rest of the suite — which is exactly why they need a test of their own.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class RateLimitingTests
{
    private readonly PostgresContainerFixture _postgres;

    public RateLimitingTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Repeated failed sign-ins are cut off, so a stolen e-mail address cannot
    /// be paired with an unlimited stream of password guesses.
    /// </summary>
    [Fact]
    public async Task Repeated_sign_in_attempts_are_throttled()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        const int permitLimit = 10;
        var statuses = new List<HttpStatusCode>();

        // One request past the limit is enough to prove the gate closes.
        for (var attempt = 0; attempt < permitLimit + 1; attempt++)
        {
            using var response = await AuthTestClient.LoginAsync(
                client,
                "yok@ornek.test",
                "yanlis-parola",
                Cancellation);

            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses[..permitLimit], status => Assert.Equal(HttpStatusCode.Unauthorized, status));
    }

    [Fact]
    public async Task A_throttled_response_is_a_turkish_problem_document()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        HttpStatusCode lastStatus = HttpStatusCode.OK;
        string body = string.Empty;

        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var response = await AuthTestClient.LoginAsync(
                client,
                "yok@ornek.test",
                "yanlis-parola",
                Cancellation);

            lastStatus = response.StatusCode;

            if (lastStatus == HttpStatusCode.TooManyRequests)
            {
                body = await response.Content.ReadAsStringAsync(Cancellation);
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
        Assert.Contains("request.rate_limited", body, StringComparison.Ordinal);
        Assert.Contains("Çok fazla", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Registration is limited far more tightly than sign-in: a legitimate
    /// person creates an account once, so a lower ceiling costs them nothing and
    /// makes bulk account creation impractical.
    /// </summary>
    [Fact]
    public async Task Bulk_registration_is_throttled()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        const int permitLimit = 5;
        HttpStatusCode lastStatus = HttpStatusCode.OK;

        for (var attempt = 0; attempt < permitLimit + 1; attempt++)
        {
            using var response = await AuthTestClient.RegisterAsync(
                client,
                AuthTestClient.UniqueEmail(),
                Cancellation);

            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}
