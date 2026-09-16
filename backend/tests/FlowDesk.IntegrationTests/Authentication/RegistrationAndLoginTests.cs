using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Authentication.RegisterUser;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Authentication;

[Collection(IntegrationTestSuite.Name)]
public sealed class RegistrationAndLoginTests
{
    private readonly PostgresContainerFixture _postgres;

    public RegistrationAndLoginTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registration_returns_an_access_token_and_sets_the_refresh_cookie()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = AuthTestClient.UniqueEmail();

        using var response = await AuthTestClient.RegisterAsync(client, email, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await AuthTestClient.ReadSessionAsync(response, Cancellation);
        Assert.NotEmpty(session.AccessToken);
        Assert.Equal(email, session.User.Email);
        Assert.NotNull(AuthTestClient.ReadRefreshCookie(response));
    }

    /// <summary>
    /// The refresh token is the one long-lived credential in the system. It
    /// must leave the server only in a cookie the browser cannot read.
    /// </summary>
    [Fact]
    public async Task Refresh_token_never_appears_in_the_response_body()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var cookieValue = AuthTestClient.ReadRefreshCookie(response);
        var body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.NotNull(cookieValue);
        Assert.DoesNotContain(cookieValue, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_cookie_is_http_only_and_scoped_to_the_auth_endpoints()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("flowdesk_refresh_token=", StringComparison.Ordinal));

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registering_an_address_that_is_already_taken_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = AuthTestClient.UniqueEmail();

        using var first = await AuthTestClient.RegisterAsync(client, email, Cancellation);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await AuthTestClient.RegisterAsync(client, email, Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(
            "auth.email_already_registered",
            await AuthTestClient.ReadProblemCodeAsync(second, Cancellation));
    }

    [Theory]
    [InlineData("gecersiz-eposta", AuthTestClient.ValidPassword)]
    [InlineData("gecerli@ornek.test", "kisa")]
    [InlineData("", AuthTestClient.ValidPassword)]
    public async Task Registration_rejects_malformed_input(string email, string password)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new RegisterUserCommand(email, "Mehmet Kaya", password),
            Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Login_succeeds_with_the_correct_password()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = AuthTestClient.UniqueEmail();

        using var registration = await AuthTestClient.RegisterAsync(client, email, Cancellation);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        using var login = await AuthTestClient.LoginAsync(
            client,
            email,
            AuthTestClient.ValidPassword,
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await AuthTestClient.ReadSessionAsync(login, Cancellation);
        Assert.Equal(email, session.User.Email);
    }

    /// <summary>
    /// A wrong password and an unknown address must be indistinguishable from
    /// outside. Any difference turns sign-in into a way to discover which
    /// addresses are registered (docs/SECURITY.md).
    /// </summary>
    [Fact]
    public async Task Wrong_password_and_unknown_address_produce_the_same_rejection()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var registeredEmail = AuthTestClient.UniqueEmail();

        using var registration = await AuthTestClient.RegisterAsync(client, registeredEmail, Cancellation);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        using var wrongPassword = await AuthTestClient.LoginAsync(
            client,
            registeredEmail,
            "tamamen-yanlis-parola",
            Cancellation);

        using var unknownAccount = await AuthTestClient.LoginAsync(
            client,
            AuthTestClient.UniqueEmail("bilinmeyen"),
            AuthTestClient.ValidPassword,
            Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAccount.StatusCode);

        var wrongPasswordCode = await AuthTestClient.ReadProblemCodeAsync(wrongPassword, Cancellation);
        var unknownAccountCode = await AuthTestClient.ReadProblemCodeAsync(unknownAccount, Cancellation);

        Assert.Equal("auth.invalid_credentials", wrongPasswordCode);
        Assert.Equal(wrongPasswordCode, unknownAccountCode);
    }

    [Fact]
    public async Task Current_user_requires_a_token()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var response = await client.GetAsync(new Uri("/api/me", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Current_user_returns_the_signed_in_account()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();
        var email = AuthTestClient.UniqueEmail();

        using var registration = await AuthTestClient.RegisterAsync(
            client,
            email,
            Cancellation,
            displayName: "Selin Arslan");

        var session = await AuthTestClient.ReadSessionAsync(registration, Cancellation);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var response = await client.GetAsync(new Uri("/api/me", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(Cancellation);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal("Selin Arslan", user.DisplayName);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var client = factory.CreateApiClient();

        using var registration = await AuthTestClient.RegisterAsync(
            client,
            AuthTestClient.UniqueEmail(),
            Cancellation);

        var session = await AuthTestClient.ReadSessionAsync(registration, Cancellation);

        // Flip the last character of the signature.
        var tampered = session.AccessToken[..^1] + (session.AccessToken[^1] == 'a' ? 'b' : 'a');

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        using var response = await client.GetAsync(new Uri("/api/me", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
