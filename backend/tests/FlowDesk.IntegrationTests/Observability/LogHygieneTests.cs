using System.Net;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace FlowDesk.IntegrationTests.Observability;

/// <summary>
/// What the running API writes to its log, checked against
/// docs/SECURITY.md §12.
/// </summary>
/// <remarks>
/// Read from the host's own logging pipeline rather than from the code that
/// writes it. A rule about what must never be logged is broken by accident —
/// an exception carrying a token, a new line added in a hurry, a framework
/// default — and only the output shows it.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class LogHygieneTests
{
    private readonly PostgresContainerFixture _postgres;

    public LogHygieneTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Kimlik_ve_davet_akislarinda_hicbir_sir_loglanmaz()
    {
        var logs = new CapturedLogs();

        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILogEventSink>(logs)));

        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });

        // The flows that carry secrets: a password, an access token, a refresh
        // cookie and a one-time invitation link.
        var email = AuthTestClient.UniqueEmail("hijyen");

        using var registration = await AuthTestClient.RegisterAsync(client, email, Cancellation);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);

        var session = await AuthTestClient.ReadSessionAsync(registration, Cancellation);
        var refreshCookie = AuthTestClient.ReadRefreshCookie(registration);
        Assert.NotNull(refreshCookie);

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        var workspace = await WorkspaceTestClient.CreateOwnedAsync(client, Cancellation);

        var invitation = await TeamTestClient.InviteAndReadAsync(
            client, workspace.Slug, AuthTestClient.UniqueEmail("davetli"), MembershipRole.Agent,
            Cancellation);

        using (var wrongPassword = await AuthTestClient.LoginAsync(
            client, email, "yanlis-parola-2026", Cancellation))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        }

        using (var refreshed = await AuthTestClient.RefreshAsync(client, Cancellation))
        {
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        }

        using (var signedOut = await AuthTestClient.LogoutAsync(client, Cancellation))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        }

        /*
          First that the sink saw anything at all. Without this the assertions
          below would pass just as happily against a host that logged nothing,
          which is the one way this test could quietly stop meaning anything.
        */
        Assert.True(
            logs.Rendered.Any(line => line.Contains("/api/auth/register", StringComparison.Ordinal)),
            $"İstek logu yazılmamış. Yakalanan olay sayısı: {logs.Events.Count}.");

        /*
          And that the search can find what is there: the workspace address
          appears in the request path of every scoped call. Without this, a
          Mentions() that always answered "no" would make the checks below
          look like a clean bill of health.
        */
        Assert.True(logs.Mentions(workspace.Slug), "Arama, logda var olan bir değeri bulamadı.");

        var secrets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["parola"] = AuthTestClient.ValidPassword,
            ["access token"] = session.AccessToken,
            ["refresh çerezi"] = refreshCookie,
            ["davet token'ı"] = invitation.Token,
            ["e-posta adresi"] = email,
        };

        var leaked = secrets
            .Where(secret => logs.Mentions(secret.Value))
            .Select(secret => secret.Key)
            .ToList();

        Assert.True(leaked.Count == 0, "Loglara sızan bilgiler: " + string.Join(", ", leaked));

        // The header itself, not just its value: a request-logging change that
        // started dumping headers would show up here.
        Assert.DoesNotContain(
            logs.Rendered,
            line => line.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
    }
}
