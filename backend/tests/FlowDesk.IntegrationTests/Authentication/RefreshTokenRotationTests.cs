using System.Net;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.Authentication;

/// <summary>
/// The refresh-token rules from docs/SECURITY.md, exercised end to end.
/// </summary>
/// <remarks>
/// These are the tests that make the session model worth having. Rotation
/// without replay detection is only bookkeeping; what protects a stolen token
/// is that its second use takes the whole family down.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class RefreshTokenRotationTests
{
    private readonly PostgresContainerFixture _postgres;

    public RefreshTokenRotationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Refreshing_issues_a_new_token_pair()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var registration = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var firstRefreshCookie = AuthTestClient.ReadRefreshCookie(registration);

        using var refresh = await AuthTestClient.RefreshAsync(client, Cancellation);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var rotatedCookie = AuthTestClient.ReadRefreshCookie(refresh);
        var session = await AuthTestClient.ReadSessionAsync(refresh, Cancellation);

        Assert.NotNull(rotatedCookie);
        Assert.NotEqual(firstRefreshCookie, rotatedCookie);
        Assert.NotEmpty(session.AccessToken);
    }

    /// <summary>
    /// The core replay rule: a refresh token is single use, and presenting a
    /// spent one revokes every token in its family — including the one the
    /// legitimate client is currently holding.
    /// </summary>
    [Fact]
    public async Task Replaying_a_spent_token_revokes_the_whole_family()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var registration = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var stolenToken = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(stolenToken);

        // The legitimate client refreshes once, spending the first token.
        using var legitimateRefresh = await AuthTestClient.RefreshAsync(client, Cancellation);
        Assert.Equal(HttpStatusCode.OK, legitimateRefresh.StatusCode);

        // An attacker replays the copy they took earlier.
        using var attackerClient = factory.CreateApiClient();
        AddRefreshCookie(attackerClient, stolenToken);

        using var replay = await AuthTestClient.RefreshAsync(attackerClient, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(
            "auth.session_revoked",
            await AuthTestClient.ReadProblemCodeAsync(replay, Cancellation));

        // And the legitimate client is signed out too: we cannot tell which
        // side was the thief, so the safe reading is that the family is burned.
        using var afterReplay = await AuthTestClient.RefreshAsync(client, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    [Fact]
    public async Task A_spent_token_cannot_be_used_even_without_a_replay()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var registration = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var firstToken = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(firstToken);

        using var refresh = await AuthTestClient.RefreshAsync(client, Cancellation);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        using var reuseClient = factory.CreateApiClient();
        AddRefreshCookie(reuseClient, firstToken);

        using var reuse = await AuthTestClient.RefreshAsync(reuseClient, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refreshing_without_a_cookie_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await AuthTestClient.RefreshAsync(client, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "auth.session_not_found",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        AddRefreshCookie(client, "uydurulmus-bir-token-degeri");

        using var response = await AuthTestClient.RefreshAsync(client, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Expiry is asserted by ageing the stored row rather than by waiting
    /// fourteen days.
    /// </summary>
    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = AuthTestClient.UniqueEmail();

        using var registration = await AuthTestClient.RegisterAsync(client, email, Cancellation);
        var token = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(token);

        await using (var dbContext = _postgres.CreateDbContext())
        {
            var user = await dbContext.Users.SingleAsync(u => u.Email == email, Cancellation);

            await dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.UserId == user.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        refreshToken => refreshToken.ExpiresAt,
                        DateTimeOffset.UtcNow.AddDays(-1)),
                    Cancellation);
        }

        using var response = await AuthTestClient.RefreshAsync(client, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "auth.session_expired",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task Signing_out_ends_the_session_server_side()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var registration = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var token = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(token);

        using var logout = await AuthTestClient.LogoutAsync(client, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // A copy of the cookie taken before sign-out must not still work.
        using var afterLogout = factory.CreateApiClient();
        AddRefreshCookie(afterLogout, token);

        using var refresh = await AuthTestClient.RefreshAsync(afterLogout, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Signing_out_without_a_session_still_succeeds()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await AuthTestClient.LogoutAsync(client, Cancellation);

        // The caller wanted to be signed out and is signed out. Reporting an
        // error would also confirm whether a token value exists.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Only a hash is persisted, so a database leak hands out nothing usable.
    /// </summary>
    [Fact]
    public async Task The_raw_token_is_never_stored()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = AuthTestClient.UniqueEmail();

        using var registration = await AuthTestClient.RegisterAsync(client, email, Cancellation);
        var rawToken = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(rawToken);

        await using var dbContext = _postgres.CreateDbContext();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email, Cancellation);

        var storedHashes = await dbContext.RefreshTokens
            .Where(token => token.UserId == user.Id)
            .Select(token => token.TokenHash)
            .ToListAsync(Cancellation);

        Assert.NotEmpty(storedHashes);
        Assert.DoesNotContain(rawToken, storedHashes);
        // SHA-256 rendered as lowercase hex.
        Assert.All(storedHashes, hash => Assert.Equal(64, hash.Length));
    }

    private static void AddRefreshCookie(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Add("Cookie", $"flowdesk_refresh_token={token}");
}
