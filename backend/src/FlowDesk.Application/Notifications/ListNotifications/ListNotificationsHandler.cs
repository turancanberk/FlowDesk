using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Notifications.ListNotifications;

/// <summary>
/// The caller's own notifications in this workspace.
/// </summary>
/// <remarks>
/// Always the caller's own. There is no parameter for whose feed to read,
/// because there is no such thing as someone else's — a notification is
/// addressed to one person and nobody else has a reason to see it, not even an
/// owner (docs/SECURITY.md).
/// </remarks>
public sealed class ListNotificationsHandler
{
    /// <summary>
    /// How many come back.
    /// </summary>
    /// <remarks>
    /// Not paged. A notification feed is read from the top and abandoned a few
    /// lines down; paging through old notices is not something anyone does, and
    /// the unread count covers the case where there are more than fit.
    /// </remarks>
    private const int FeedLimit = 30;

    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListNotificationsHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<NotificationFeed>> HandleAsync(CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.View))
        {
            return Result.Failure<NotificationFeed>(
                TenancyErrors.InsufficientRole("Bildirimleri görüntülemek"));
        }

        var userId = _tenantContext.UserId;

        // Scoped to the workspace by the query filter, and to the person here.
        var mine = _dbContext.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == userId);

        var unreadCount = await mine.CountAsync(
            notification => notification.ReadAt == null, cancellationToken);

        var items = await mine
            // Unread first, then newest. The two together are what makes the
            // feed useful: what needs attention, in the order it arrived.
            .OrderBy(notification => notification.ReadAt != null)
            .ThenByDescending(notification => notification.CreatedAt)
            .Take(FeedLimit)
            .Select(notification => new NotificationItem(
                notification.Id,
                notification.Type,
                notification.Payload,
                notification.ReadAt != null,
                notification.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new NotificationFeed(items, unreadCount));
    }
}
