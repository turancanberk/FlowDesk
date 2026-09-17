using System.Text.Json;
using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Notifications;
using FlowDesk.Application.Notifications.ListNotifications;
using FlowDesk.Application.Notifications.MarkNotificationsRead;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var notifications = endpoints
            .MapGroup($"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}/notifications")
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Notifications");

        notifications.MapGet("/", ListAsync).WithName("ListNotifications");

        /*
          POST rather than PATCH on each notification. Marking read is an action
          on a set — opening the panel marks several at once — and one request
          per row would be a burst of writes for something nobody watches
          (docs/API_CONVENTIONS.md).
        */
        notifications.MapPost("/read", MarkReadAsync).WithName("MarkNotificationsRead");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ListNotificationsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> MarkReadAsync(
        [FromBody] MarkNotificationsReadRequest request,
        MarkNotificationsReadHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new MarkNotificationsReadCommand(request?.NotificationIds ?? []),
            cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static NotificationFeedResponse ToResponse(NotificationFeed feed) =>
        new(
            [.. feed.Items.Select(item => new NotificationResponse(
                item.Id,
                item.Type,
                // Parsed here rather than stored parsed: the application layer
                // passes the payload through untouched, and turning it into an
                // object is a transport concern.
                JsonDocument.Parse(item.Payload).RootElement.Clone(),
                item.IsRead,
                item.CreatedAt))],
            feed.UnreadCount);
}
