using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Api.Tenancy;
using FlowDesk.Application.Tickets;
using FlowDesk.Application.Tickets.AddTicketComment;
using FlowDesk.Application.Tickets.AssignTicket;
using FlowDesk.Application.Tickets.ChangeTicketStatus;
using FlowDesk.Application.Tickets.CreateTicket;
using FlowDesk.Application.Tickets.DeleteAttachment;
using FlowDesk.Application.Tickets.DownloadAttachment;
using FlowDesk.Application.Tickets.ListAttachments;
using FlowDesk.Application.Tickets.UploadAttachment;
using FlowDesk.Application.Tickets.DeleteTicket;
using FlowDesk.Application.Tickets.GetTicket;
using FlowDesk.Application.Tickets.ListTicketComments;
using FlowDesk.Application.Tickets.ListTickets;
using FlowDesk.Application.Tickets.UpdateTicket;
using FlowDesk.Domain.Tickets;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Api.Endpoints;

public static class TicketEndpoints
{
    /// <summary>
    /// The hard ceiling on an upload request body.
    /// </summary>
    /// <remarks>
    /// Sits above the configured file limit on purpose: multipart framing adds
    /// its own bytes, and a file exactly at the product's limit must not be cut
    /// off by the transport before the product can answer for it.
    /// </remarks>
    private const long UploadRequestSizeLimitBytes = 32L * 1024 * 1024;

    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var tickets = endpoints
            .MapGroup($"/api/workspaces/{{{WorkspaceResolutionFilter.RouteParameterName}}}/tickets")
            .RequireAuthorization()
            .AddEndpointFilter<WorkspaceResolutionFilter>()
            .WithTags("Tickets");

        tickets.MapGet("/", ListAsync).WithName("ListTickets");
        tickets.MapGet("/{ticketId:guid}", GetAsync).WithName("GetTicket");

        tickets.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateTicketCommand>>()
            .WithName("CreateTicket");

        tickets.MapPatch("/{ticketId:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateTicketCommand>>()
            .WithName("UpdateTicket");

        tickets.MapDelete("/{ticketId:guid}", DeleteAsync).WithName("DeleteTicket");

        /*
          Status and assignment are actions on their own routes rather than
          fields of the PATCH body. A status change is a state-machine
          transition whose validity depends on where the ticket stands, and an
          assignment of null means "unassign" — a meaning PATCH cannot express,
          because an absent field and a null field are not reliably
          distinguishable (docs/API_CONVENTIONS.md).
        */
        tickets.MapPost("/{ticketId:guid}/status", ChangeStatusAsync).WithName("ChangeTicketStatus");
        tickets.MapPost("/{ticketId:guid}/assignment", AssignAsync).WithName("AssignTicket");

        tickets.MapGet("/{ticketId:guid}/comments", ListCommentsAsync).WithName("ListTicketComments");

        tickets.MapPost("/{ticketId:guid}/comments", AddCommentAsync)
            .AddEndpointFilter<ValidationFilter<AddTicketCommentCommand>>()
            .WithName("AddTicketComment");

        tickets.MapGet("/{ticketId:guid}/attachments", ListAttachmentsAsync)
            .WithName("ListAttachments");

        tickets.MapPost("/{ticketId:guid}/attachments", UploadAttachmentAsync)
            .WithName("UploadAttachment")
            /*
              The body limit is set here as well as in the use case, and the two
              are not redundant. The use case refuses an oversized file; this
              stops the server reading one in the first place, which is what a
              limit is for. Without it a large upload is absorbed in full and
              only then rejected.

              Deliberately a little above the configured limit, so a file just
              over the product's rule meets the product's error message rather
              than the server's connection reset.
            */
            .WithMetadata(new RequestSizeLimitAttribute(UploadRequestSizeLimitBytes))
            .DisableAntiforgery();

        tickets.MapGet("/{ticketId:guid}/attachments/{attachmentId:guid}", DownloadAttachmentAsync)
            .WithName("DownloadAttachment");

        tickets.MapDelete("/{ticketId:guid}/attachments/{attachmentId:guid}", DeleteAttachmentAsync)
            .WithName("DeleteAttachment");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ListTicketsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] TicketStatus? status = null,
        [FromQuery] TicketPriority? priority = null,
        [FromQuery] Guid? customerId = null,
        [FromQuery] Guid? assignedUserId = null,
        [FromQuery] bool unassigned = false,
        [FromQuery] TicketSort sort = TicketSort.RecentlyUpdated,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        var result = await handler.HandleAsync(
            new ListTicketsQuery(
                search, status, priority, customerId, assignedUserId, unassigned, sort, page, pageSize),
            cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(PagedResponse.From(result.Value, ToResponse));
    }

    private static async Task<IResult> GetAsync(
        Guid ticketId,
        GetTicketHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateTicketCommand command,
        CreateTicketHandler createHandler,
        GetTicketHandler getHandler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var created = await createHandler.HandleAsync(command, cancellationToken);

        if (created.IsFailure)
        {
            return created.Error.ToProblem(httpContext);
        }

        // Read back through the query path so the created ticket is described
        // exactly as a later GET would describe it, including its number and
        // resolved names.
        var detail = await getHandler.HandleAsync(created.Value, cancellationToken);

        if (detail.IsFailure)
        {
            return detail.Error.ToProblem(httpContext);
        }

        var slug = httpContext.Request.RouteValues[WorkspaceResolutionFilter.RouteParameterName];

        return Results.Created(
            $"/api/workspaces/{slug}/tickets/{created.Value}",
            ToResponse(detail.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid ticketId,
        [FromBody] UpdateTicketCommand command,
        UpdateTicketHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid ticketId,
        [FromBody] ChangeTicketStatusCommand command,
        ChangeTicketStatusHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> AssignAsync(
        Guid ticketId,
        [FromBody] AssignTicketCommand command,
        AssignTicketHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    private static async Task<IResult> DeleteAsync(
        Guid ticketId,
        DeleteTicketHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static async Task<IResult> ListCommentsAsync(
        Guid ticketId,
        ListTicketCommentsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(result.Value.Select(ToResponse).ToList());
    }

    private static async Task<IResult> AddCommentAsync(
        Guid ticketId,
        [FromBody] AddTicketCommentCommand command,
        AddTicketCommentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        var slug = httpContext.Request.RouteValues[WorkspaceResolutionFilter.RouteParameterName];

        return Results.Created(
            $"/api/workspaces/{slug}/tickets/{ticketId}/comments/{result.Value.Id}",
            ToResponse(result.Value));
    }

    private static async Task<IResult> ListAttachmentsAsync(
        Guid ticketId,
        ListAttachmentsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(result.Value.Select(ToResponse).ToList());
    }

    private static async Task<IResult> UploadAttachmentAsync(
        Guid ticketId,
        IFormFile file,
        UploadAttachmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Results.BadRequest();
        }

        await using var content = file.OpenReadStream();

        var result = await handler.HandleAsync(
            ticketId,
            new UploadAttachmentCommand(file.FileName, file.ContentType, file.Length, content),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        var slug = httpContext.Request.RouteValues[WorkspaceResolutionFilter.RouteParameterName];

        return Results.Created(
            $"/api/workspaces/{slug}/tickets/{ticketId}/attachments/{result.Value.Id}",
            ToResponse(result.Value));
    }

    private static async Task<IResult> DownloadAttachmentAsync(
        Guid ticketId,
        Guid attachmentId,
        DownloadAttachmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, attachmentId, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.ToProblem(httpContext);
        }

        /*
          Always an attachment, never inline. Serving a file from our own origin
          in a way the browser renders would let an uploaded document run in the
          context of the application — which is why SVG is off the allow-list to
          begin with, and why nothing here is left to the browser's judgement
          (docs/SECURITY.md).
        */
        return Results.File(
            result.Value.Content,
            result.Value.ContentType,
            fileDownloadName: result.Value.FileName);
    }

    private static async Task<IResult> DeleteAttachmentAsync(
        Guid ticketId,
        Guid attachmentId,
        DeleteAttachmentHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ticketId, attachmentId, cancellationToken);

        return result.IsFailure ? result.Error.ToProblem(httpContext) : Results.NoContent();
    }

    private static AttachmentResponse ToResponse(AttachmentItem attachment) =>
        new(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeInBytes,
            attachment.UploadedByUserId,
            attachment.CreatedAt);

    private static TicketListItemResponse ToResponse(TicketListItem ticket) =>
        new(
            ticket.Id,
            ticket.Number,
            ticket.Subject,
            ticket.CustomerId,
            ticket.CustomerName,
            ticket.Status,
            ticket.Priority,
            ticket.AssignedUserId,
            ticket.AssignedUserDisplayName,
            ticket.CreatedAt,
            ticket.UpdatedAt);

    private static TicketDetailResponse ToResponse(TicketDetail ticket) =>
        new(
            ticket.Id,
            ticket.Number,
            ticket.Subject,
            ticket.Description,
            ticket.CustomerId,
            ticket.CustomerName,
            ticket.Status,
            ticket.Priority,
            ticket.AssignedUserId,
            ticket.AssignedUserDisplayName,
            ticket.CreatedByUserId,
            ticket.CreatedByDisplayName,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt,
            ticket.AvailableTransitions,
            ticket.Version);

    private static TicketCommentResponse ToResponse(TicketCommentItem comment) =>
        new(
            comment.Id,
            comment.AuthorUserId,
            comment.AuthorDisplayName,
            comment.Body,
            comment.CreatedAt);
}
