using FlowDesk.Application.Abstractions;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Handles one kind of integration message.
/// </summary>
/// <remarks>
/// Lives in Infrastructure rather than Application because consuming is a
/// hosting concern: it exists only because a broker delivers work, where a use
/// case exists because a person asked for something. A consumer's job is to
/// deserialise, resolve the right use case and call it.
///
/// <para>
/// <b>Delivery is at-least-once.</b> The broker resends anything it is not sure
/// was handled, so an implementation must be able to see the same message twice
/// and act once. Faz 11 adds a processed-message table to make that automatic;
/// until then each consumer is responsible for its own idempotency.
/// </para>
/// </remarks>
public interface IMessageConsumer<in TMessage>
    where TMessage : IntegrationMessage
{
    Task HandleAsync(TMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// What the host needs to know about a consumer without knowing its type.
/// </summary>
/// <remarks>
/// The generic interface is what an implementation writes; this is what the
/// background service iterates over. Keeping them apart means a consumer never
/// deals with raw bytes and the host never deals with message types.
/// </remarks>
public interface IMessageSubscription
{
    /// <summary>The queue this subscription reads from.</summary>
    string QueueName { get; }

    /// <summary>
    /// The routing pattern the queue binds to, such as <c>ticket.*</c>.
    /// </summary>
    string RoutingPattern { get; }

    /// <summary>
    /// Deserialises the body and hands it to the consumer.
    /// </summary>
    /// <remarks>
    /// Takes a scope factory rather than a resolved consumer: each message is
    /// handled in its own dependency-injection scope, so a <c>DbContext</c> is
    /// not shared between two deliveries and its change tracker cannot carry
    /// one message's entities into the next.
    /// </remarks>
    Task DispatchAsync(
        ReadOnlyMemory<byte> body,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken);
}
