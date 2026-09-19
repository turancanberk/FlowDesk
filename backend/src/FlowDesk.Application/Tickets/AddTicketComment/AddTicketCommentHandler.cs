using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.AddTicketComment;

public sealed class AddTicketCommentHandler
{
    /// <summary>How much of the comment travels in the notification line.</summary>
    private const int ExcerptLength = 160;

    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly IUserAccountStore _accountStore;
    private readonly IMessagePublisher _publisher;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public AddTicketCommentHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        IMessagePublisher publisher,
        ITenantContext tenantContext,
        IClock clock,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _publisher = publisher;
        _tenantContext = tenantContext;
        _clock = clock;
        _activity = activity;
    }

    public async Task<Result<TicketCommentItem>> HandleAsync(
        Guid ticketId,
        AddTicketCommentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.CommentOnTickets))
        {
            return Result.Failure<TicketCommentItem>(TicketErrors.CommentNotAllowed);
        }

        /*
          Confirms the ticket exists in this workspace before writing a comment
          against its id, and reads the fields the notification needs while it
          is here. Skipping the check would let a caller attach a comment to a
          ticket id from another organisation.
        */
        var ticket = await _dbContext.Tickets
            .AsNoTracking()
            .Where(candidate => candidate.Id == ticketId)
            .Select(candidate => new { candidate.Number, candidate.Subject, candidate.AssignedUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketCommentItem>(TicketErrors.NotFound);
        }

        var authorId = _tenantContext.UserId;
        var now = _clock.UtcNow;

        var comment = TicketComment.Create(
            _tenantContext.TenantId,
            ticketId,
            authorId,
            command.Body,
            now);

        _dbContext.TicketComments.Add(comment);

        // Queued before saving, so the comment and the message commit together
        // (ADR-0033).
        await _publisher.PublishAsync(
            new TicketCommented(
                Guid.CreateVersion7(),
                _tenantContext.TenantId,
                now,
                ticketId,
                ticket.Number,
                ticket.Subject,
                authorId,
                ticket.AssignedUserId,
                Excerpt(comment.Body),
                _tenantContext.Slug),
            cancellationToken);

        /*
          The comment's text is deliberately not recorded. The history says that
          someone commented; what they wrote lives on the ticket, where it can
          be edited or removed. Copying it here would make the audit trail a
          second, unremovable copy of every remark anyone ever made.
        */
        _activity.Record(
            ActivityType.TicketCommented,
            ActivitySubject.Ticket,
            ticketId,
            new TicketActivityPayload(ticket.Number, ticket.Subject));

        await _dbContext.SaveChangesAsync(cancellationToken);

        var author = await _accountStore.FindByIdAsync(authorId, cancellationToken);

        return Result.Success(new TicketCommentItem(
            comment.Id,
            comment.AuthorUserId,
            author?.DisplayName ?? "Bilinmeyen kullanıcı",
            comment.Body,
            comment.CreatedAt));
    }

    /// <summary>
    /// The opening of a comment, for a one-line notification.
    /// </summary>
    /// <remarks>
    /// Cut at a word boundary where there is one nearby, so the line ends on a
    /// word rather than mid-syllable — which in Turkish can turn a fragment
    /// into a different word entirely.
    /// </remarks>
    private static string Excerpt(string body)
    {
        if (body.Length <= ExcerptLength)
        {
            return body;
        }

        var cut = body[..ExcerptLength];
        var lastSpace = cut.LastIndexOf(' ');

        // Only if the boundary is near the end; otherwise a long unbroken run
        // would be cut back to almost nothing.
        if (lastSpace > ExcerptLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut + "…";
    }
}
