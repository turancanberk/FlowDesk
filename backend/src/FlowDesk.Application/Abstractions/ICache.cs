namespace FlowDesk.Application.Abstractions;

/// <summary>
/// A short-lived store for values that can be recomputed.
/// </summary>
/// <remarks>
/// Everything here is derived data. Nothing is stored that could not be worked
/// out again from the database, which is what makes the next point safe:
///
/// <para>
/// <b>A cache failure is never an error.</b> If the store is unreachable, a
/// read misses and a write is dropped, and the caller carries on as though the
/// cache were empty. A product that stops working because its cache is down has
/// turned an optimisation into a dependency (ADR-0037).
/// </para>
/// </remarks>
public interface ICache
{
    /// <summary>
    /// Reads a value, or null when it is absent — or when the cache is
    /// unreachable, which the caller cannot and need not tell apart.
    /// </summary>
    Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        where TValue : class;

    /// <summary>
    /// Stores a value for <paramref name="ttl"/>.
    /// </summary>
    /// <remarks>
    /// Always with an expiry. A cached value with no lifetime is a second copy
    /// of the truth that nothing is obliged to correct.
    /// </remarks>
    Task SetAsync<TValue>(
        string key,
        TValue value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where TValue : class;

    /// <summary>Drops a key, if it is there.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

/// <summary>
/// The keys FlowDesk caches under.
/// </summary>
/// <remarks>
/// Built here rather than at each call site, because the one thing that must
/// never be wrong is the workspace in them. A key without it would serve one
/// organisation's figures to another — the single real risk in caching this
/// data, and exactly the kind of mistake a string literal invites
/// (ADR-0037).
/// </remarks>
public static class CacheKeys
{
    public static string Dashboard(Guid tenantId) => $"tenant:{tenantId:N}:dashboard";
}
