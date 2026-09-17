using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Tickets;
using FlowDesk.Domain.Notifications;
using FlowDesk.Infrastructure.Email;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Notifications;

/// <summary>
/// Tells someone a ticket is now theirs: in the app, and by e-mail.
/// </summary>
/// <remarks>
/// The order matters. The notification row is written first and the e-mail is
/// sent last, because the row is inside the transaction and the e-mail is not.
/// If the send fails, the transaction rolls back and the message is redelivered
/// — nothing has been sent and nothing has been recorded. If it were the other
/// way round, a failure after sending would redeliver and send again
/// (ADR-0034).
/// </remarks>
public sealed class TicketAssignedConsumer : IMessageConsumer<TicketAssigned>
{
    public const string QueueName = "flowdesk.ticket-assigned.notify";

    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly IClock _clock;

    public TicketAssignedConsumer(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(emailOptions);

        _dbContext = dbContext;
        _accountStore = accountStore;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _clock = clock;
    }

    public async Task HandleAsync(TicketAssigned message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Nobody needs telling about something they just did themselves.
        if (message.AssignedUserId == message.AssignedByUserId)
        {
            return;
        }

        var assignee = await _accountStore.FindByIdAsync(message.AssignedUserId, cancellationToken);

        if (assignee is null)
        {
            // The account went away between the assignment and now. Returning
            // quietly marks the message handled; retrying would never succeed.
            return;
        }

        var assignedBy = await _accountStore.FindByIdAsync(
            message.AssignedByUserId, cancellationToken);

        var payload = new TicketAssignedPayload(
            message.TicketId,
            message.TicketNumber,
            message.Subject,
            assignedBy?.DisplayName ?? "Bir ekip üyesi",
            message.WorkspaceSlug);

        /*
          The TenantId comes from the message. A consumer has no workspace
          context of its own, so this is the only thing that scopes what it
          writes (ADR-0033).
        */
        _dbContext.Notifications.Add(Notification.Create(
            message.TenantId,
            message.AssignedUserId,
            NotificationType.TicketAssigned,
            JsonSerializer.Serialize(payload, FlowDeskMessageJson.Options),
            _clock.UtcNow));

        // Saved before the send, so the row exists in this transaction and a
        // failing send rolls it back with everything else.
        await _dbContext.SaveChangesAsync(cancellationToken);

        var ticketNumber = $"TLP-{message.TicketNumber}";
        var link = $"{_emailOptions.WebBaseUrl.TrimEnd('/')}" +
                   $"/app/{message.WorkspaceSlug}/tickets/{message.TicketId}";

        await _emailSender.SendAsync(
            new EmailMessage(
                assignee.Email,
                assignee.DisplayName,
                $"{ticketNumber} size atandı",
                EmailBodies.Html(
                    $"{ticketNumber} size atandı",
                    $"{payload.AssignedByDisplayName}, <strong>{EmailBodies.Escape(message.Subject)}</strong> " +
                    "konulu talebi size atadı.",
                    "Talebi aç",
                    link),
                EmailBodies.Text(
                    $"{payload.AssignedByDisplayName}, \"{message.Subject}\" konulu talebi size atadı.",
                    link)),
            cancellationToken);
    }
}
