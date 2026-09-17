using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Messaging;

/// <summary>
/// A record that one consumer has already handled one message.
/// </summary>
/// <remarks>
/// Delivery is at-least-once: the broker resends anything it is not sure was
/// handled, so a consumer will see the same message twice. This row is how it
/// notices. Written in the same transaction as the work it describes, so
/// "handled" and "recorded as handled" cannot come apart.
///
/// <para>
/// The key is the message <b>and</b> the consumer, not the message alone. Two
/// consumers may legitimately act on the same message — one sends an e-mail,
/// another writes a notification — and a key on the message alone would let the
/// first to finish silently suppress the second.
/// </para>
///
/// <para>
/// Deliberately not <c>ITenantOwned</c>, for the same reason as
/// <see cref="OutboxMessage"/>: a consumer runs outside any request and has no
/// workspace context to filter by (ADR-0024).
/// </para>
/// </remarks>
public sealed class ProcessedMessage
{
    public const int MaximumConsumerLength = 200;

    private ProcessedMessage()
    {
        Consumer = string.Empty;
    }

    private ProcessedMessage(Guid messageId, string consumer, DateTimeOffset processedAt)
    {
        MessageId = messageId;
        Consumer = consumer;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }

    /// <summary>Which consumer handled it.</summary>
    public string Consumer { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }

    public static ProcessedMessage Create(Guid messageId, string consumer, DateTimeOffset processedAt)
    {
        if (messageId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A processed message must name a message.");
        }

        var trimmed = (consumer ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRuleViolationException("A processed message must name its consumer.");
        }

        if (trimmed.Length > MaximumConsumerLength)
        {
            throw new DomainRuleViolationException(
                $"A consumer name cannot exceed {MaximumConsumerLength} characters.");
        }

        return new ProcessedMessage(messageId, trimmed, processedAt);
    }
}
