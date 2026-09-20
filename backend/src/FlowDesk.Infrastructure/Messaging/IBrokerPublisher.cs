namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Hands an already-serialised message to the broker.
/// </summary>
/// <remarks>
/// Internal to infrastructure and deliberately untyped. Only the outbox
/// processor calls it, and the processor never knows a message type — it moves
/// bytes it read from a row. Keeping this separate from
/// <c>IMessagePublisher</c> is what lets the application contract mean "queue
/// this" while the broker contract means "send this now" (ADR-0033).
/// </remarks>
public interface IBrokerPublisher
{
    /// <param name="traceParent">
    /// W3C trace context to travel with the message, so the consumer's work
    /// continues the trace of the request that caused it (ADR-0042).
    /// </param>
    Task PublishAsync(
        Guid messageId,
        string messageType,
        string routingKey,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload,
        string? traceParent,
        CancellationToken cancellationToken);
}
