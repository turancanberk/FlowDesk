using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Dashboard;
using FlowDesk.Application.Dashboard.GetDashboard;

namespace FlowDesk.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet(
                $"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}/dashboard",
                GetAsync)
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Dashboard")
            .WithName("GetDashboard");

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        GetDashboardHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static DashboardResponse ToResponse(DashboardSummary summary) =>
        new(
            summary.CustomerCount,
            summary.OpenTicketCount,
            summary.UnassignedTicketCount,
            summary.OverdueTaskCount,
            summary.DueSoonTaskCount,
            summary.MemberCount,
            [.. summary.TicketsByStatus.Select(entry =>
                new TicketStatusCountResponse(entry.Status, entry.Count))],
            [.. summary.RecentTickets.Select(ticket => new RecentTicketResponse(
                ticket.Id,
                ticket.Number,
                ticket.Subject,
                ticket.CustomerName,
                ticket.Status,
                ticket.Priority,
                ticket.UpdatedAt))],
            [.. summary.UpcomingTasks.Select(task => new UpcomingTaskResponse(
                task.Id,
                task.Title,
                task.DueAt,
                task.IsOverdue,
                task.AssignedUserDisplayName))],
            summary.GeneratedAt);
}
