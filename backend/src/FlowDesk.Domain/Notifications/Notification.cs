using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Domain.Notifications;

/// <summary>
/// Something that happened which one person should know about.
/// </summary>
/// <remarks>
/// Tenant-owned, so a notification is scoped like every other business record:
/// a person who belongs to two workspaces sees each one's notifications only
/// while they are in it.
/// </remarks>
public sealed class Notification : ITenantOwned
{
    public const int MaximumPayloadLength = 4000;

    private Notification()
    {
        Payload = string.Empty;
    }

    private Notification(
        Guid id,
        Guid tenantId,
        Guid userId,
        NotificationType type,
        string payload,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Type = type;
        Payload = payload;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Who it is for.</summary>
    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    /// <summary>
    /// The context needed to render and link to it.
    /// </summary>
    /// <remarks>
    /// Stored rather than joined at read time, so a notification still reads
    /// correctly after the thing it refers to has changed — or been deleted. A
    /// notification is a record of a moment, not a live view of a ticket.
    /// </remarks>
    public string Payload { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Notification Create(
        Guid tenantId,
        Guid userId,
        NotificationType type,
        string payload,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A notification must belong to a workspace.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A notification must have a recipient.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleViolationException($"'{type}' is not a valid notification type.");
        }

        var trimmed = (payload ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRuleViolationException("A notification must carry its context.");
        }

        if (trimmed.Length > MaximumPayloadLength)
        {
            throw new DomainRuleViolationException(
                $"A notification payload cannot exceed {MaximumPayloadLength} characters.");
        }

        return new Notification(Guid.CreateVersion7(), tenantId, userId, type, trimmed, createdAt);
    }

    /// <summary>
    /// Marks it read.
    /// </summary>
    /// <remarks>
    /// Idempotent, and keeps the first time. Reading something twice does not
    /// make it newly read, and overwriting the timestamp would quietly reset
    /// how long it sat unread.
    /// </remarks>
    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
    }
}
