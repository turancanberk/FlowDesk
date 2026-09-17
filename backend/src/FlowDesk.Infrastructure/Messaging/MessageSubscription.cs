using System.Text.Json;
using FlowDesk.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Binds one message type to its queue, its routing pattern and its consumer.
/// </summary>
/// <remarks>
/// A queue per message type rather than one shared queue. A shared queue makes
/// a slow consumer block every other kind of message behind it, and it makes a
/// poison message a problem for handlers that never wanted it.
/// </remarks>
public sealed class MessageSubscription<TMessage> : IMessageSubscription
    where TMessage : IntegrationMessage
{
    public MessageSubscription(string queueName, string routingPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingPattern);

        QueueName = queueName;
        RoutingPattern = routingPattern;
    }

    public string QueueName { get; }

    public string ConsumerName => QueueName;

    public string RoutingPattern { get; }

    public async Task DispatchAsync(
        ReadOnlyMemory<byte> body,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);

        TMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<TMessage>(body.Span, FlowDeskMessageJson.Options);
        }
        catch (JsonException exception)
        {
            // Wrapped, so the host can tell an unreadable body from a handler
            // that failed: the first must not be redelivered, the second must.
            throw new MessageFormatException(
                $"'{typeof(TMessage).Name}' mesajı çözümlenemedi.", exception);
        }

        if (message is null)
        {
            throw new MessageFormatException(
                $"'{typeof(TMessage).Name}' mesajı çözümlenemedi: gövde boş JSON.");
        }

        var consumer = scopedServices.GetRequiredService<IMessageConsumer<TMessage>>();

        await consumer.HandleAsync(message, cancellationToken);
    }
}
