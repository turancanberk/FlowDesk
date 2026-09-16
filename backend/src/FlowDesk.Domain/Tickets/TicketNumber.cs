using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Tickets;

/// <summary>
/// The human-facing identifier of a ticket, such as <c>TLP-1042</c>.
/// </summary>
/// <remarks>
/// Tickets also have a <see cref="Guid"/> primary key, but nobody reads one
/// aloud on a phone call. The number is short, sequential within a workspace,
/// and stable — which is what makes it usable as the thing a team and a
/// customer refer to.
///
/// Sequential per workspace rather than globally, so one organisation cannot
/// infer another's volume from the gaps in its own numbering.
/// </remarks>
public readonly record struct TicketNumber
{
    /// <summary>Turkish for "talep" (request), kept short for readability.</summary>
    public const string Prefix = "TLP";

    /// <summary>Numbering starts high enough that the first ticket does not look like a test.</summary>
    public const int FirstNumber = 1001;

    public TicketNumber(int value)
    {
        if (value < FirstNumber)
        {
            throw new DomainRuleViolationException(
                $"A ticket number must be at least {FirstNumber}.");
        }

        Value = value;
    }

    public int Value { get; }

    /// <summary>Renders as <c>TLP-1042</c>.</summary>
    public override string ToString() => $"{Prefix}-{Value}";

    public static implicit operator int(TicketNumber number) => number.Value;
}
