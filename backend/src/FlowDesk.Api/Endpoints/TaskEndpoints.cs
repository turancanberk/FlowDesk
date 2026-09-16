using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Tasks;
using FlowDesk.Application.Tasks.ChangeTaskStatus;
using FlowDesk.Application.Tasks.CreateTask;
using FlowDesk.Application.Tasks.DeleteTask;
using FlowDesk.Application.Tasks.GetTask;
using FlowDesk.Application.Tasks.ListTasks;
using FlowDesk.Application.Tasks.UpdateTask;
using FlowDesk.Domain.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

public static class TaskEndpoints
{
    public static IEndpointRouteBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var tasks = endpoints
            .MapGroup($"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}/tasks")
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Tasks");

        tasks.MapGet("/", ListAsync).WithName("ListTasks");
        tasks.MapGet("/{taskId:guid}", GetAsync).WithName("GetTask");

        tasks.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateTaskCommand>>()
            .WithName("CreateTask");

        // Replaces every editable field, so null means "none" rather than
        // "leave alone" — the ambiguity a partial body would carry.
        tasks.MapPatch("/{taskId:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateTaskCommand>>()
            .WithName("UpdateTask");

        // Its own route so that ticking a checkbox in the list is one request
        // rather than a fetch followed by a full update
        // (docs/API_CONVENTIONS.md).
        tasks.MapPost("/{taskId:guid}/status", ChangeStatusAsync).WithName("ChangeTaskStatus");

        tasks.MapDelete("/{taskId:guid}", DeleteAsync).WithName("DeleteTask");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ListTasksHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] TaskItemStatus? status = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? assignedUserId = null,
        [FromQuery] bool unassigned = false,
        [FromQuery] bool overdue = false,
        [FromQuery] TaskSort sort = TaskSort.DueSoonest,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var result = await handler.HandleAsync(
            new ListTasksQuery(
                search, status, customerId, assignedUserId, unassigned, overdue, sort, page, pageSize),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(PagedResponse.From(result.Value, ToResponse));
    }

    private static async Task<IResult> GetAsync(
        Guid taskId,
        GetTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(taskId, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateTaskCommand command,
        CreateTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        var slug = httpContext.Request.RouteValues[WorkspaceResolutionFilter.RouteParameterName];

        return Results.Created(
            $"/api/workspaces/{slug}/tasks/{result.Value.Id}",
            ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid taskId,
        [FromBody] UpdateTaskCommand command,
        UpdateTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(taskId, command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid taskId,
        [FromBody] ChangeTaskStatusCommand command,
        ChangeTaskStatusHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(taskId, command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> DeleteAsync(
        Guid taskId,
        DeleteTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(taskId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static TaskListItemResponse ToResponse(TaskListItem task) =>
        new(
            task.Id,
            task.Title,
            task.Status,
            task.DueAt,
            task.IsOverdue,
            task.AssignedUserId,
            task.AssignedUserDisplayName,
            task.CustomerId,
            task.CustomerName,
            task.CreatedAt,
            task.UpdatedAt);

    private static TaskDetailResponse ToResponse(TaskDetail task) =>
        new(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.DueAt,
            task.IsOverdue,
            task.AssignedUserId,
            task.AssignedUserDisplayName,
            task.CustomerId,
            task.CustomerName,
            task.CreatedByUserId,
            task.CreatedByDisplayName,
            task.CreatedAt,
            task.UpdatedAt,
            task.CompletedAt);
}
