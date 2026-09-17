using System.Text.Json;
using System.Text.Json.Serialization;
using FlowDesk.Domain.Notifications;

namespace FlowDesk.Api.Contracts;

/// <param name="Payload">
/// Passed through as JSON rather than a string, so the client reads an object
/// instead of parsing a string that happens to contain one.
/// </param>
public sealed record NotificationResponse(
    Guid Id,
    NotificationType Type,
    JsonElement Payload,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationFeedResponse(
    IReadOnlyList<NotificationResponse> Items,
    int UnreadCount);

/// <param name="NotificationIds">
/// Omit or send an empty list to mark everything read.
/// </param>
public sealed record MarkNotificationsReadRequest(
    [property: JsonPropertyName("notificationIds")] IReadOnlyCollection<Guid>? NotificationIds);
