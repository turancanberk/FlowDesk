using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Tickets;
using FlowDesk.Domain.Notifications;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Notifications;

/// <summary>
/// Tells the person a ticket belongs to that someone wrote on it.
/// </summary>
/// <remarks>
/// In the app only, no e-mail. Assignment is a handover and warrants an
/// interruption; a comment on a ticket someone is already working on does not,
/// and a mail per comment is the fastest way to teach people to filter FlowDesk
/// out of their inbox.
/// </remarks>
public sealed class TicketCommentedConsumer : IMessageConsumer<TicketCommented>
{
    public const string QueueName = "flowdesk.ticket-commented.notify";

    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly IClock _clock;

    public TicketCommentedConsumer(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _clock = clock;
    }

    public async Task HandleAsync(TicketCommented message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Nobody to tell, or the commenter is the person it belongs to. The
        // recipient is who held the ticket when the comment was written, as
        // the message records it — not whoever holds it now.
        if (message.AssigneeUserId is not { } recipient || recipient == message.AuthorUserId)
        {
            return;
        }

        /*
          A ticket deleted before the message arrived leaves nothing to point
          the notice at. Read without the workspace filter, and scoped by hand
          instead: the consumer has no workspace context, and the TenantId from
          the message is what keeps this to one organisation's ticket
          (ADR-0033).
        */
        var ticketStillExists = await _dbContext.Tickets
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                ticket => ticket.Id == message.TicketId && ticket.TenantId == message.TenantId,
                cancellationToken);

        if (!ticketStillExists)
        {
            return;
        }

        var author = await _accountStore.FindByIdAsync(message.AuthorUserId, cancellationToken);

        var payload = new TicketCommentedPayload(
            message.TicketId,
            message.TicketNumber,
            message.Subject,
            author?.DisplayName ?? "Bir ekip üyesi",
            message.Excerpt,
            message.WorkspaceSlug);

        _dbContext.Notifications.Add(Notification.Create(
            message.TenantId,
            recipient,
            NotificationType.TicketCommented,
            JsonSerializer.Serialize(payload, FlowDeskMessageJson.Options),
            _clock.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
