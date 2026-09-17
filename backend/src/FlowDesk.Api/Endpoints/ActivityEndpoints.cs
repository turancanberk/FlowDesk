using System.Text.Json;
using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Activity.ListActivity;
using FlowDesk.Domain.Activity;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet(
                $"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}/activity",
                ListAsync)
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Activity")
            .WithName("ListActivity");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ListActivityHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        [FromQuery] ActivitySubject? subjectType = null,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] ActivityType? type = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var result = await handler.HandleAsync(
            new ListActivityQuery(subjectType, subjectId, type, actorUserId, page, pageSize),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(PagedResponse.From(result.Value, ToResponse));
    }

    private static ActivityResponse ToResponse(ActivityItem item) =>
        new(
            item.Id,
            item.ActorUserId,
            item.ActorDisplayName,
            item.Type,
            item.SubjectType,
            item.SubjectId,
            // Parsed here rather than stored parsed: the application layer
            // passes the payload through untouched.
            JsonDocument.Parse(item.Payload).RootElement.Clone(),
            item.OccurredAt);
}
