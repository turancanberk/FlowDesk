namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Publishes an integration message for something that has already happened.
/// </summary>
/// <remarks>
/// The application states that a fact should leave the process; which broker
/// carries it, and how, is infrastructure's business (ADR-0001).
///
/// Publishing is not transactional with the database. A message sent from
/// inside a transaction that later rolls back describes something that never
/// happened, and one sent after a commit can be lost if the process dies in
/// between. Faz 11 closes that gap with an outbox; until then callers publish
/// after committing and accept that a crash in the window loses the message.
/// </remarks>
public interface IMessagePublisher
{
    /// <summary>
    /// Sends <paramref name="message"/> under its own routing key.
    /// </summary>
    /// <param name="message">
    /// Serialised as JSON. Its <see cref="IntegrationMessage.MessageId"/> is
    /// what lets a consumer recognise a redelivery.
    /// </param>
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IntegrationMessage;
}

/// <summary>
/// The shape every integration message shares.
/// </summary>
/// <remarks>
/// A base record rather than an interface, so a message cannot be published
/// without an id and a timestamp. Delivery is at-least-once: the broker will
/// resend anything it is not sure was handled, so a consumer must be able to
/// see the same message twice and act once. The id is how it tells.
/// </remarks>
/// <param name="MessageId">
/// Unique per publication, not per event. Republishing the same fact after a
/// failure produces a new id, which is correct: the consumer is deciding
/// whether it has handled <em>this delivery</em>.
/// </param>
/// <param name="TenantId">
/// Carried on every message. A consumer runs outside a request and has no
/// workspace context of its own, so anything it writes has to be told which
/// organisation it belongs to (docs/SECURITY.md).
/// </param>
public abstract record IntegrationMessage(
    Guid MessageId,
    Guid TenantId,
    DateTimeOffset OccurredAt)
{
    /// <summary>
    /// The routing key this message is published under, such as
    /// <c>ticket.assigned</c>.
    /// </summary>
    /// <remarks>
    /// Declared by the message rather than passed at the call site, so the same
    /// fact cannot be published under two different keys by two callers.
    /// </remarks>
    public abstract string RoutingKey { get; }
}
