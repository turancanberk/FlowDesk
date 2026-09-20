using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FlowDesk.Infrastructure.Caching;

/// <summary>
/// A cache backed by Redis.
/// </summary>
/// <remarks>
/// Every operation swallows its failures on purpose. A cache that throws makes
/// the product depend on it, and this product does not: a miss and an outage
/// look the same to the caller, which is the whole point (ADR-0037).
///
/// <para>
/// Failures are logged rather than passed over silently, so an outage is
/// visible to an operator even though it is invisible to a user.
/// </para>
/// </remarks>
public sealed partial class RedisCache : ICache
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisCache> _logger;

    public RedisCache(IConnectionMultiplexer connection, ILogger<RedisCache> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        where TValue : class
    {
        try
        {
            var value = await _connection.GetDatabase().StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                CountRead(FlowDeskTelemetry.Outcomes.Miss);

                return null;
            }

            CountRead(FlowDeskTelemetry.Outcomes.Hit);

            // Cast to string: RedisValue converts implicitly to several types, and
            // the overload set is ambiguous without one.
            return JsonSerializer.Deserialize<TValue>((string)value!, FlowDeskMessageJson.Options);
        }
#pragma warning disable CA1031 // A cache miss and a cache outage are the same
        // thing to the caller; letting either escape would make an optimisation
        // into a dependency.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            /*
              Counted apart from a miss. To the caller they are the same thing
              (ADR-0037), but to whoever is watching they are not: misses are
              normal, errors mean Redis is unwell.
            */
            CountRead(FlowDeskTelemetry.Outcomes.Error);

            LogReadFailed(_logger, key, exception);

            return null;
        }
    }

    public async Task SetAsync<TValue>(
        string key,
        TValue value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where TValue : class
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, FlowDeskMessageJson.Options);

            await _connection.GetDatabase().StringSetAsync(key, payload, ttl);
        }
#pragma warning disable CA1031 // A failed write simply means the next read misses.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogWriteFailed(_logger, key, exception);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _connection.GetDatabase().KeyDeleteAsync(key);
        }
#pragma warning disable CA1031 // A failed removal leaves a value that expires
        // on its own; the TTL is what bounds the damage.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogRemoveFailed(_logger, key, exception);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Önbellekten okunamadı: {Key}. İstek önbelleksiz sürdürülüyor.")]
    private static partial void LogReadFailed(ILogger logger, string key, Exception exception);

    private static void CountRead(string result) =>
        FlowDeskTelemetry.CacheReads.Add(1, new KeyValuePair<string, object?>("result", result));

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Önbelleğe yazılamadı: {Key}. Sonraki okuma ıskalayacak.")]
    private static partial void LogWriteFailed(ILogger logger, string key, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Önbellek kaydı silinemedi: {Key}. Kayıt kendi süresiyle düşecek.")]
    private static partial void LogRemoveFailed(ILogger logger, string key, Exception exception);
}
