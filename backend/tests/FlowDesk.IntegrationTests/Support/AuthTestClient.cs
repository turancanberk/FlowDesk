using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Authentication.LoginUser;
using FlowDesk.Application.Authentication.RegisterUser;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>A registered user together with an authenticated client.</summary>
internal sealed record SignedInUser(HttpClient Client, string Email, Guid Id) : IDisposable
{
    public void Dispose() => Client.Dispose();
}

/// <summary>
/// Helpers for driving the authentication endpoints from a test.
/// </summary>
/// <remarks>
/// Goes through the real HTTP surface rather than calling handlers directly, so
/// cookie flags, model binding and the middleware pipeline are all part of what
/// is being verified.
/// </remarks>
internal static class AuthTestClient
{
    /// <summary>Meets the ten-character minimum without being a realistic secret.</summary>
    public const string ValidPassword = "gecerli-parola-2026";

    /// <summary>
    /// A fresh address per call. Tests share one database, and colliding on a
    /// unique index would make failures depend on execution order.
    /// </summary>
    public static string UniqueEmail(string prefix = "kullanici") =>
        $"{prefix}.{Guid.CreateVersion7():N}@ornek.test";

    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        CancellationToken cancellationToken,
        string displayName = "Ayşe Demir",
        string password = ValidPassword) =>
        client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new RegisterUserCommand(email, displayName, password),
            cancellationToken);

    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginUserCommand(email, password),
            cancellationToken);

    public static Task<HttpResponseMessage> RefreshAsync(
        HttpClient client,
        CancellationToken cancellationToken) =>
        client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), content: null, cancellationToken);

    public static Task<HttpResponseMessage> LogoutAsync(
        HttpClient client,
        CancellationToken cancellationToken) =>
        client.PostAsync(new Uri("/api/auth/logout", UriKind.Relative), content: null, cancellationToken);

    public static async Task<AuthenticationResponse> ReadSessionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var session = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(cancellationToken);

        Assert.NotNull(session);

        return session;
    }

    /// <summary>Reads the refresh cookie the server just set, if any.</summary>
    public static string? ReadRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        const string prefix = "flowdesk_refresh_token=";

        var cookie = cookies.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.Ordinal));

        if (cookie is null)
        {
            return null;
        }

        var value = cookie[prefix.Length..].Split(';')[0];

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Registers a user and returns a client already carrying their bearer
    /// token, so a test can get to the thing it is actually asserting.
    /// </summary>
    public static async Task<SignedInUser> SignInNewUserAsync(
        FlowDeskApiFactory factory,
        CancellationToken cancellationToken,
        string displayName = "Ahmet Yılmaz")
    {
        var client = factory.CreateApiClient();
        var email = UniqueEmail();

        using var response = await RegisterAsync(client, email, cancellationToken, displayName);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        var session = await ReadSessionAsync(response, cancellationToken);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        return new SignedInUser(client, email, session.User.Id);
    }

    /// <summary>Reads the <c>code</c> extension that ProblemDetails responses carry.</summary>
    public static async Task<string?> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(
            cancellationToken);

        return problem is not null && problem.TryGetValue("code", out var code)
            ? code.GetString()
            : null;
    }
}
