using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Tickets;

/// <summary>
/// The per-workspace ticket numbering counter.
/// </summary>
/// <remarks>
/// A table row rather than a database sequence, for two reasons. A sequence per
/// workspace would mean creating schema objects at runtime, and sequences are
/// not transactional — a rolled-back ticket creation would still consume a
/// number and leave a visible gap.
///
/// The row is locked inside the same transaction that inserts the ticket, so
/// two simultaneous creations cannot take the same number. The unique index on
/// <c>(TenantId, Number)</c> is the last line of defence if that ever fails.
/// </remarks>
public sealed class TenantCounter
{
    private TenantCounter()
    {
    }

    private TenantCounter(Guid tenantId, int nextTicketNumber)
    {
        TenantId = tenantId;
        NextTicketNumber = nextTicketNumber;
    }

    /// <summary>Also the primary key: one counter row per workspace.</summary>
    public Guid TenantId { get; private set; }

    public int NextTicketNumber { get; private set; }

    public static TenantCounter StartFor(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A counter must belong to a workspace.");
        }

        return new TenantCounter(tenantId, TicketNumber.FirstNumber);
    }

    /// <summary>Takes the next number and advances the counter.</summary>
    public int TakeNextTicketNumber()
    {
        var number = NextTicketNumber;
        NextTicketNumber = number + 1;

        return number;
    }
}
