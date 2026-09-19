using FlowDesk.Application.Team;
using FlowDesk.Application.Tickets;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.Infrastructure.Notifications;

/// <summary>
/// The consumers that turn integration messages into notices and mail.
/// </summary>
/// <remarks>
/// Kept here rather than in the Worker's Program so the end-to-end test can
/// start exactly the set the Worker runs. A copy in the test would keep passing
/// after a consumer went missing from the Worker — which is the mistake worth
/// catching.
/// </remarks>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the notification consumers and the host that runs them.
    /// </summary>
    /// <remarks>
    /// Consumers run in the Worker and only there. The API publishes but does
    /// not consume: if both hosts read the same queues, every message would be
    /// handled twice and the second handling would be invisible in the API's
    /// own logs.
    /// </remarks>
    public static IServiceCollection AddFlowDeskNotificationConsumers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddFlowDeskMessageConsumers();

        /*
          A queue per message type, and the queue name is also the consumer's
          name in the processed-message table (ADR-0033) — so these names are a
          contract, not a label. Renaming one makes every message it has already
          handled look unhandled.
        */
        services.AddMessageConsumer<TicketAssigned, TicketAssignedConsumer>(
            queueName: TicketAssignedConsumer.QueueName,
            routingPattern: TicketAssigned.Key);

        services.AddMessageConsumer<TicketCommented, TicketCommentedConsumer>(
            queueName: TicketCommentedConsumer.QueueName,
            routingPattern: TicketCommented.Key);

        services.AddMessageConsumer<MemberInvited, MemberInvitedConsumer>(
            queueName: MemberInvitedConsumer.QueueName,
            routingPattern: MemberInvited.Key);

        return services;
    }
}
