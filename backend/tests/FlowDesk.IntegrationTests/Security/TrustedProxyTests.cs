using System.Net;
using FlowDesk.IntegrationTests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.IntegrationTests.Security;

/// <summary>
/// Who the rate limiter thinks the caller is (docs/SECURITY.md §10).
/// </summary>
/// <remarks>
/// In production one proxy serves the frontend and the API from a single
/// origin, so every request arrives from its address. Two failures are
/// possible and both are serious: believing <c>X-Forwarded-For</c> from
/// anyone lets a caller choose their own bucket, and ignoring it behind the
/// proxy puts the whole internet in one bucket.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TrustedProxyTests
{
    private const string FirstClient = "203.0.113.10";
    private const string SecondClient = "203.0.113.11";

    private readonly PostgresContainerFixture _postgres;

    public TrustedProxyTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Guvenilen_proxy_tanimli_degilse_baslik_yok_sayilir()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        await using var host = WithClientAddress(factory, trustedProxy: null);
        using var client = host.CreateClient();

        // One registration allowed. If the header were believed, the second
        // caller would look like somebody else and get a permit of their own.
        using var first = await RegisterAsync(client, FirstClient);
        using var second = await RegisterAsync(client, SecondClient);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task Guvenilen_proxy_arkasinda_her_istemci_kendi_kovasini_alir()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        await using var host = WithClientAddress(factory, trustedProxy: "127.0.0.1");
        using var client = host.CreateClient();

        using var first = await RegisterAsync(client, FirstClient);
        using var second = await RegisterAsync(client, SecondClient);
        using var firstAgain = await RegisterAsync(client, FirstClient);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Their own bucket, and their own limit: the first caller is spent.
        Assert.Equal(HttpStatusCode.TooManyRequests, firstAgain.StatusCode);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string clientAddress)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/auth/register", UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                email = AuthTestClient.UniqueEmail("proxy"),
                displayName = "Proxy Arkası",
                password = AuthTestClient.ValidPassword,
            }),
        };

        request.Headers.Add("X-Forwarded-For", clientAddress);

        return client.SendAsync(request, Cancellation);
    }

    /// <summary>
    /// A host that answers as though the connection came from the loopback
    /// address, which is what a proxy on the same machine looks like.
    /// </summary>
    /// <remarks>
    /// The test server leaves the remote address unset, so without this every
    /// caller would share the "unknown" partition and the test would prove
    /// nothing either way.
    /// </remarks>
    private static WebApplicationFactory<Program> WithClientAddress(
        FlowDeskApiFactory factory,
        string? trustedProxy) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:RegistrationPermitLimit", "1");

            if (trustedProxy is not null)
            {
                builder.UseSetting("Network:TrustedProxies:0", trustedProxy);
            }

            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new ConnectionAddressFilter(IPAddress.Loopback)));
        });

    private sealed class ConnectionAddressFilter : IStartupFilter
    {
        private readonly IPAddress _address;

        public ConnectionAddressFilter(IPAddress address) => _address = address;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            builder =>
            {
                // Before everything, including the forwarded headers
                // middleware that decides whether to believe the header.
                builder.Use(async (context, continuation) =>
                {
                    context.Connection.RemoteIpAddress = _address;

                    await continuation(context);
                });

                next(builder);
            };
    }
}
