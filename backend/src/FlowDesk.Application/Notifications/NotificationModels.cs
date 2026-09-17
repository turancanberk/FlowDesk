using FlowDesk.Domain.Notifications;

namespace FlowDesk.Application.Notifications;

/// <param name="Payload">
/// Raw JSON, passed through to the client. The application does not interpret
/// it: what a notification looks like is a presentation decision, and parsing
/// it here would mean a server change every time a line of text moved.
/// </param>
public sealed record NotificationItem(
    Guid Id,
    NotificationType Type,
    string Payload,
    bool IsRead,
    DateTimeOffset CreatedAt);

/// <param name="UnreadCount">
/// Counted across everything, not just the page. The badge has to be right even
/// when there are more notices than anyone will scroll through.
/// </param>
public sealed record NotificationFeed(
    IReadOnlyList<NotificationItem> Items,
    int UnreadCount);
