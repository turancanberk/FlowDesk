using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Domain.Activity;

/// <summary>
/// A record that something happened in a workspace.
/// </summary>
/// <remarks>
/// Append-only. There is no method to change or remove one, and that is the
/// point: a history that can be edited answers no question worth asking. Rows
/// go only when the workspace itself does.
///
/// <para>
/// The payload carries display context and <b>never</b> anything sensitive — no
/// password, no token, no invitation link, no message body. An audit trail is
/// read by more people than the thing it describes, and for longer
/// (docs/SECURITY.md).
/// </para>
/// </remarks>
public sealed class ActivityEvent : ITenantOwned
{
    public const int MaximumPayloadLength = 4000;

    private ActivityEvent()
    {
        Payload = string.Empty;
    }

    private ActivityEvent(
        Guid id,
        Guid tenantId,
        Guid? actorUserId,
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        string payload,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        ActorUserId = actorUserId;
        Type = type;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// Who did it, or null when the system did.
    /// </summary>
    /// <remarks>
    /// Nullable because not every event has a person behind it — an invitation
    /// expiring, a scheduled clean-up. "Nobody" is a real answer and is better
    /// than attributing it to whoever happened to trigger the check.
    /// </remarks>
    public Guid? ActorUserId { get; private set; }

    public ActivityType Type { get; private set; }

    public ActivitySubject SubjectType { get; private set; }

    /// <summary>
    /// Which record it was about.
    /// </summary>
    /// <remarks>
    /// Not a foreign key. The subject may be deleted and the event has to
    /// outlive it — an audit trail that loses the record of a deletion when the
    /// thing is deleted is exactly backwards.
    /// </remarks>
    public Guid SubjectId { get; private set; }

    /// <summary>Context for rendering the line, with nothing sensitive in it.</summary>
    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static ActivityEvent Record(
        Guid tenantId,
        Guid? actorUserId,
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        string payload,
        DateTimeOffset occurredAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("An activity event must belong to a workspace.");
        }

        if (actorUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "Use null for a system event rather than an empty actor.");
        }

        if (subjectId == Guid.Empty)
        {
            throw new DomainRuleViolationException("An activity event must name its subject.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleViolationException($"'{type}' is not a valid activity type.");
        }

        if (!Enum.IsDefined(subjectType))
        {
            throw new DomainRuleViolationException($"'{subjectType}' is not a valid subject type.");
        }

        var trimmed = (payload ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRuleViolationException("An activity event must carry its context.");
        }

        if (trimmed.Length > MaximumPayloadLength)
        {
            throw new DomainRuleViolationException(
                $"An activity payload cannot exceed {MaximumPayloadLength} characters.");
        }

        return new ActivityEvent(
            Guid.CreateVersion7(),
            tenantId,
            actorUserId,
            type,
            subjectType,
            subjectId,
            trimmed,
            occurredAt);
    }
}
