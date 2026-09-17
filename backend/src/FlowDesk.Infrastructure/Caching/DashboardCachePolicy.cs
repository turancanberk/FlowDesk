using FlowDesk.Application.Dashboard;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Caching;

/// <summary>Reads the configured dashboard lifetime for the application layer.</summary>
public sealed class DashboardCachePolicy : IDashboardCachePolicy
{
    private readonly CacheOptions _options;

    public DashboardCachePolicy(IOptions<CacheOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public TimeSpan DashboardTtl => TimeSpan.FromSeconds(_options.DashboardTtlSeconds);
}
