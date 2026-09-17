using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Notifications.MarkNotificationsRead;

public sealed class MarkNotificationsReadHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public MarkNotificationsReadHandler(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result> HandleAsync(
        MarkNotificationsReadCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.View))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Bildirimleri görüntülemek"));
        }

        var userId = _tenantContext.UserId;

        /*
          The UserId condition is not decoration. Without it, naming another
          person's notification id would mark theirs read — a small harm, but a
          real one, and exactly the kind of check that gets left out because the
          workspace filter looks like it is already doing the work. It is not:
          the filter scopes to the workspace, and colleagues share one.
        */
        var pending = _dbContext.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null);

        if (command.NotificationIds.Count > 0)
        {
            var ids = command.NotificationIds.ToArray();
            pending = pending.Where(notification => ids.Contains(notification.Id));
        }

        var notifications = await pending.ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;

        foreach (var notification in notifications)
        {
            notification.MarkRead(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
