using Testcontainers.Redis;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Starts a real Redis once for the whole test run.
/// </summary>
/// <remarks>
/// Real rather than an in-memory stand-in, for the same reason the other
/// services are. What these tests check is that a value written under one
/// workspace's key is never read under another's, and that an unreachable cache
/// changes nothing a user sees — behaviour that belongs to the store and to the
/// code around it, not to a substitute.
/// </remarks>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private const string RedisImage = "redis:8-alpine";

    // The image goes to the constructor: the parameterless overload is obsolete.
    private readonly RedisContainer _container = new RedisBuilder(RedisImage)
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
