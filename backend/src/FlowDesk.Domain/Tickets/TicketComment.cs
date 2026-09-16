using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Domain.Tickets;

/// <summary>A note left on a ticket by a team member.</summary>
public sealed class TicketComment : ITenantOwned
{
    public const int MaximumBodyLength = 4000;

    private TicketComment()
    {
        Body = string.Empty;
    }

    private TicketComment(
        Guid id,
        Guid tenantId,
        Guid ticketId,
        Guid authorUserId,
        string body,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        TicketId = ticketId;
        AuthorUserId = authorUserId;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TicketComment Create(
        Guid tenantId,
        Guid ticketId,
        Guid authorUserId,
        string body,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A comment must belong to a workspace.");
        }

        if (ticketId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A comment must belong to a ticket.");
        }

        if (authorUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A comment must record its author.");
        }

        var trimmedBody = (body ?? string.Empty).Trim();

        if (trimmedBody.Length == 0)
        {
            throw new DomainRuleViolationException("A comment cannot be empty.");
        }

        if (trimmedBody.Length > MaximumBodyLength)
        {
            throw new DomainRuleViolationException(
                $"A comment cannot exceed {MaximumBodyLength} characters.");
        }

        return new TicketComment(
            Guid.CreateVersion7(),
            tenantId,
            ticketId,
            authorUserId,
            trimmedBody,
            createdAt);
    }
}
