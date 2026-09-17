using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Caching;

/// <summary>Where the cache is, and how stale it may get.</summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Empty turns caching off entirely.
    /// </summary>
    /// <remarks>
    /// Not merely allowed but supported: the product works without a cache, and
    /// a deployment that does not want one should not have to run Redis to say
    /// so. The tests rely on it too.
    /// </remarks>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// How long a dashboard may be out of date.
    /// </summary>
    /// <remarks>
    /// A minute by default. Long enough to absorb someone reloading the page or
    /// switching back to the tab, short enough that a figure someone just
    /// changed catches up before they go looking for it.
    /// </remarks>
    [Range(1, 3600)]
    public int DashboardTtlSeconds { get; set; } = 60;
}
