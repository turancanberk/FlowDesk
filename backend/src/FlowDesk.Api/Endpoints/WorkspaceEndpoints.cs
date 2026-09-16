using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Tenancy;
using FlowDesk.Application.Tenancy.CreateWorkspace;
using FlowDesk.Application.Tenancy.DeleteWorkspace;
using FlowDesk.Application.Tenancy.GetWorkspace;
using FlowDesk.Application.Tenancy.ListWorkspaces;
using FlowDesk.Application.Tenancy.UpdateWorkspace;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

/// <summary>
/// Workspace creation, listing and settings.
/// </summary>
/// <remarks>
/// Split into two groups on purpose.
///
/// The collection routes are not scoped to a workspace: creating one and
/// listing your own are things you do from outside any particular workspace.
///
/// Everything under <c>{workspaceSlug}</c> is scoped, and the whole group
/// carries <see cref="WorkspaceResolutionFilter"/>. Applying it at the group
/// keeps it from being a per-endpoint decision that a future endpoint could
/// silently skip — and skipping it would mean serving one workspace's data to
/// another's member.
/// </remarks>
public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var collection = endpoints
            .MapGroup("/api/workspaces")
            .RequireAuthorization()
            .WithTags("Workspaces");

        collection.MapGet("/", ListAsync).WithName("ListWorkspaces");

        collection.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateWorkspaceCommand>>()
            .WithName("CreateWorkspace");

        var scoped = endpoints
            .MapGroup($"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}")
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Workspaces");

        scoped.MapGet("/", GetAsync).WithName("GetWorkspace");

        scoped.MapPatch("/", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateWorkspaceCommand>>()
            .WithName("UpdateWorkspace");

        scoped.MapDelete("/", DeleteAsync).WithName("DeleteWorkspace");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ListWorkspacesHandler handler,
        CancellationToken cancellationToken)
    {
        var workspaces = await handler.HandleAsync(cancellationToken);

        return Results.Ok(workspaces.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateWorkspaceCommand command,
        CreateWorkspaceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        var workspace = ToResponse(result.Value);

        return Results.Created($"/api/workspaces/{workspace.Slug}", workspace);
    }

    private static async Task<IResult> GetAsync(
        GetWorkspaceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateWorkspaceCommand command,
        UpdateWorkspaceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> DeleteAsync(
        DeleteWorkspaceHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static WorkspaceResponse ToResponse(WorkspaceSummary workspace) =>
        new(workspace.Id, workspace.Name, workspace.Slug, workspace.Role, workspace.CreatedAt);
}
