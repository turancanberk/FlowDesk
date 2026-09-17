namespace FlowDesk.Application.Notifications.MarkNotificationsRead;

/// <param name="NotificationIds">
/// Empty marks everything the caller has unread. Naming ids marks just those,
/// which is what opening one notification does.
/// </param>
public sealed record MarkNotificationsReadCommand(IReadOnlyCollection<Guid> NotificationIds);
