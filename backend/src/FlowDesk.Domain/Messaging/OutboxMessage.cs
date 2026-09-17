using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Messaging;

/// <summary>
/// A message waiting to be handed to the broker.
/// </summary>
/// <remarks>
/// Written in the same database transaction as the change it describes, which
/// is the whole point. Publishing directly cannot be made atomic with a commit:
/// a message sent from inside a transaction that later rolls back describes
/// something that never happened, and one sent after the commit is lost if the
/// process dies in between. Writing a row removes the gap — the row and the
/// change land together or neither does.
///
/// <para>
/// Deliberately <b>not</b> <c>ITenantOwned</c>. The processor runs in the
/// worker, outside any request, and has no workspace context; enrolling this in
/// the global query filter would make it find nothing at all (ADR-0024). The
/// tenant still travels inside the payload, because the consumer needs it.
/// </para>
/// </remarks>
public sealed class OutboxMessage
{
    public const int MaximumErrorLength = 4000;

    private OutboxMessage()
    {
        Type = string.Empty;
        RoutingKey = string.Empty;
        Payload = string.Empty;
    }

    private OutboxMessage(
        Guid id,
        string type,
        string routingKey,
        string payload,
        DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        RoutingKey = routingKey;
        Payload = payload;
        OccurredAt = occurredAt;
        NextAttemptAt = occurredAt;
    }

    /// <summary>
    /// Also the id the message carries to the broker.
    /// </summary>
    /// <remarks>
    /// One id rather than two, so a consumer deciding whether it has already
    /// handled something is asking about the same row an operator would look up.
    /// </remarks>
    public Guid Id { get; private set; }

    /// <summary>The message type's name, for diagnostics and for reading the table.</summary>
    public string Type { get; private set; }

    /// <summary>
    /// The key the message is published under.
    /// </summary>
    /// <remarks>
    /// Stored rather than derived from <see cref="Type"/>. Deriving it would
    /// need a registry mapping names back to types, kept in step by hand; with
    /// the key on the row the processor never has to know a single message type
    /// and simply moves bytes.
    /// </remarks>
    public string RoutingKey { get; private set; }

    /// <summary>The serialised message, exactly as it will reach the broker.</summary>
    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Set once the broker has accepted the message.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    public int AttemptCount { get; private set; }

    /// <summary>
    /// Why the last attempt failed.
    /// </summary>
    /// <remarks>
    /// Kept on the row rather than only in the log, so that "why is this stuck"
    /// is answerable from the table an operator is already looking at.
    /// </remarks>
    public string? LastError { get; private set; }

    /// <summary>
    /// When this row becomes eligible again.
    /// </summary>
    /// <remarks>
    /// Null once processed. A failed attempt pushes it forward, so a broker that
    /// is down does not turn into a tight retry loop against it.
    /// </remarks>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        string type,
        string routingKey,
        string payload,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleViolationException("An outbox message must have an id.");
        }

        Require(type, nameof(type));
        Require(routingKey, nameof(routingKey));
        Require(payload, nameof(payload));

        return new OutboxMessage(id, type, routingKey, payload, occurredAt);
    }

    /// <summary>Records that the broker accepted the message.</summary>
    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        AttemptCount++;
        LastError = null;
        // Cleared so the partial index on pending rows stops carrying this one.
        NextAttemptAt = null;
    }

    /// <summary>
    /// Records a failed attempt and schedules the next one.
    /// </summary>
    /// <param name="retryAfter">
    /// How long to wait. The caller owns the back-off curve, because how long to
    /// wait is an operational decision rather than a rule about the message.
    /// </param>
    public void MarkFailed(string error, TimeSpan retryAfter, DateTimeOffset now)
    {
        AttemptCount++;

        var text = (error ?? string.Empty).Trim();

        // Truncated rather than rejected: a provider's exception text can run to
        // pages, and losing the row over its own error message would be worse
        // than losing the tail of the message.
        LastError = text.Length > MaximumErrorLength
            ? text[..MaximumErrorLength]
            : text;

        NextAttemptAt = now + retryAfter;
    }

    private static void Require(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleViolationException($"An outbox message must have a {fieldName}.");
        }
    }
}
