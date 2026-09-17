using FlowDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowDesk.Infrastructure.Caching;

/// <summary>
/// Drops a workspace's cached dashboard whenever anything in it changes.
/// </summary>
/// <remarks>
/// Here rather than in each handler on purpose. Nearly every write moves a
/// figure on that screen — a customer, a ticket, a task, a member — so spreading
/// the call across twenty handlers would mean twenty chances to forget it, and
/// the symptom of forgetting is a number that stays wrong until its own
/// lifetime runs out.
///
/// <para>
/// An interceptor is the designed place for this: it runs after a save that
/// actually changed something, and it keeps the context itself free of any
/// knowledge that a cache exists (ADR-0037).
/// </para>
///
/// <para>
/// This is a refinement, not a substitute for the lifetime. The key still
/// expires on its own, which is what covers a write that happens in another
/// process — the worker, another instance — where this interceptor never runs.
/// </para>
/// </remarks>
public sealed class DashboardCacheInvalidator : SaveChangesInterceptor
{
    private readonly ICache _cache;
    private readonly ITenantContext _tenantContext;

    public DashboardCacheInvalidator(ICache cache, ITenantContext tenantContext)
    {
        _cache = cache;
        _tenantContext = tenantContext;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        /*
          Only when a workspace is in scope and something was actually written.
          The worker saves with no workspace resolved, and a save that changed
          nothing has nothing to invalidate.
        */
        if (result > 0 && _tenantContext.IsResolved)
        {
            await _cache.RemoveAsync(
                CacheKeys.Dashboard(_tenantContext.TenantId), cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
