using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Team;
using FlowDesk.Application.Team.AcceptInvitation;
using FlowDesk.Application.Team.ChangeMemberRole;
using FlowDesk.Application.Team.InviteMember;
using FlowDesk.Application.Team.ListInvitations;
using FlowDesk.Application.Team.ListMembers;
using FlowDesk.Application.Team.RemoveMember;
using FlowDesk.Application.Team.RevokeInvitation;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

/// <summary>
/// Team membership and invitations.
/// </summary>
public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var scoped = endpoints
            .MapGroup($"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}")
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Team");

        scoped.MapGet("/members", ListMembersAsync).WithName("ListMembers");
        scoped.MapPatch("/members/{userId:guid}", ChangeMemberRoleAsync).WithName("ChangeMemberRole");
        scoped.MapDelete("/members/{userId:guid}", RemoveMemberAsync).WithName("RemoveMember");

        scoped.MapGet("/invitations", ListInvitationsAsync).WithName("ListInvitations");

        scoped.MapPost("/invitations", InviteMemberAsync)
            .AddEndpointFilter<ValidationFilter<InviteMemberCommand>>()
            .WithName("InviteMember");

        scoped.MapDelete("/invitations/{invitationId:guid}", RevokeInvitationAsync)
            .WithName("RevokeInvitation");

        /*
          Acceptance sits outside the workspace scope on purpose: the person
          accepting is not a member yet, and the resolution filter would reject
          them before they could join. The scope comes from the token itself.
          Documented as a deliberate deviation in docs/API_CONVENTIONS.md.
        */
        endpoints.MapPost("/api/invitations/accept", AcceptInvitationAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingPolicies.InvitationAcceptance)
            .WithName("AcceptInvitation")
            .WithTags("Team");

        return endpoints;
    }

    private static async Task<IResult> ListMembersAsync(
        ListMembersHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        return Results.Ok(result.Value.Select(member => new TeamMemberResponse(
            member.UserId,
            member.Email,
            member.DisplayName,
            member.Role,
            member.JoinedAt,
            member.IsLastOwner)));
    }

    private static async Task<IResult> ChangeMemberRoleAsync(
        Guid userId,
        [FromBody] ChangeMemberRoleRequest request,
        ChangeMemberRoleHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ChangeMemberRoleCommand(userId, request.Role),
            cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid userId,
        RemoveMemberHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(userId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static async Task<IResult> ListInvitationsAsync(
        ListInvitationsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(result.Value.Select(ToResponse));
    }

    private static async Task<IResult> InviteMemberAsync(
        [FromBody] InviteMemberCommand command,
        InviteMemberHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Created(
                $"/api/workspaces/{httpContext.Request.RouteValues[WorkspaceResolutionFilter.RouteParameterName]}/invitations",
                new CreatedInvitationResponse(ToResponse(result.Value.Invitation), result.Value.Token));
    }

    private static async Task<IResult> RevokeInvitationAsync(
        Guid invitationId,
        RevokeInvitationHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(invitationId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static async Task<IResult> AcceptInvitationAsync(
        [FromBody] AcceptInvitationCommand command,
        AcceptInvitationHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(new AcceptedInvitationResponse(
                result.Value.TenantId,
                result.Value.WorkspaceSlug,
                result.Value.WorkspaceName));
    }

    private static PendingInvitationResponse ToResponse(PendingInvitation invitation) =>
        new(invitation.Id, invitation.Email, invitation.Role, invitation.CreatedAt, invitation.ExpiresAt);
}
