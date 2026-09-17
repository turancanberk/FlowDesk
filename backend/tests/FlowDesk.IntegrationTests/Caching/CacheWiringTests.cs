using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Caching;
using FlowDesk.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.IntegrationTests.Caching;

/// <summary>
/// Which cache the host resolves.
/// </summary>
/// <remarks>
/// The two implementations are indistinguishable at the call site — one stores,
/// the other does nothing — so a misconfiguration looks exactly like a cache
/// that keeps missing. Asserting the registration is what tells them apart.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class CacheWiringTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly RedisContainerFixture _redis;

    public CacheWiringTests(PostgresContainerFixture postgres, RedisContainerFixture redis)
    {
        _postgres = postgres;
        _redis = redis;
    }

    [Fact]
    public void A_configured_connection_resolves_the_redis_cache()
    {
        using var factory = new FlowDeskApiFactory(
            _postgres.ConnectionString, cacheConnectionString: _redis.ConnectionString);

        Assert.IsType<RedisCache>(factory.Services.GetRequiredService<ICache>());
    }

    [Fact]
    public void No_connection_resolves_the_null_cache()
    {
        using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        Assert.IsType<NullCache>(factory.Services.GetRequiredService<ICache>());
    }
}
