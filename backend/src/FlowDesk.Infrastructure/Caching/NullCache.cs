using FlowDesk.Application.Abstractions;

namespace FlowDesk.Infrastructure.Caching;

/// <summary>
/// A cache that stores nothing.
/// </summary>
/// <remarks>
/// Registered when no connection string is configured. It exists so that
/// "no cache" is a supported configuration rather than a broken one: every
/// caller keeps its single code path, and turning caching off is a matter of
/// leaving a setting empty rather than of branching on it (ADR-0037).
/// </remarks>
public sealed class NullCache : ICache
{
    public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        where TValue : class =>
        Task.FromResult<TValue?>(null);

    public Task SetAsync<TValue>(
        string key,
        TValue value,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where TValue : class =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
