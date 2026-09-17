using FlowDesk.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>Registers consumers and starts the host that runs them.</summary>
/// <remarks>
/// Separate from <c>AddFlowDeskInfrastructure</c>, which every host calls.
/// Publishing belongs to everyone; consuming belongs to the Worker alone, and
/// the API starting consumers would mean the same message handled twice.
/// </remarks>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Runs the registered subscriptions for as long as the host is up.
    /// </summary>
    public static IServiceCollection AddFlowDeskMessageConsumers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<MessageConsumerService>();

        return services;
    }

    /// <summary>
    /// Registers a consumer together with the queue it reads from.
    /// </summary>
    /// <param name="queueName">
    /// A queue per message type. A shared queue would let one slow consumer
    /// block every other kind of message behind it.
    /// </param>
    /// <param name="routingPattern">
    /// The pattern the queue binds to, such as <c>ticket.assigned</c> or
    /// <c>ticket.*</c>.
    /// </param>
    public static IServiceCollection AddMessageConsumer<TMessage, TConsumer>(
        this IServiceCollection services,
        string queueName,
        string routingPattern)
        where TMessage : IntegrationMessage
        where TConsumer : class, IMessageConsumer<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped, because a consumer works through use cases that need a
        // per-message DbContext; the host opens a scope for each delivery.
        services.AddScoped<IMessageConsumer<TMessage>, TConsumer>();

        services.AddSingleton<IMessageSubscription>(
            new MessageSubscription<TMessage>(queueName, routingPattern));

        return services;
    }
}
