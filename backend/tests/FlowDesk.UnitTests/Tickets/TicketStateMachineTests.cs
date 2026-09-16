using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.UnitTests.Tickets;

/// <summary>
/// The ticket status transition rules.
/// </summary>
/// <remarks>
/// Asserted over every pair of statuses rather than by sampling the interesting
/// ones. A transition table is exactly where an accidental extra entry hides:
/// nobody notices that Closed can jump straight to Resolved until a report
/// shows work being completed after it was filed away.
/// </remarks>
public sealed class TicketStateMachineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The table from docs/PRODUCT.md, written out independently here.</summary>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Expected = new()
    {
        [TicketStatus.Open] =
            [TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.InProgress] =
            [TicketStatus.Open, TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Waiting] =
            [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Resolved] =
            [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Closed],
        [TicketStatus.Closed] = [TicketStatus.Open],
    };

    [Fact]
    public void Every_status_pair_behaves_as_documented()
    {
        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                var ticket = TicketAt(from);
                var allowed = from == to || Expected[from].Contains(to);

                Assert.Equal(allowed, ticket.CanTransitionTo(to));

                if (allowed)
                {
                    ticket.ChangeStatus(to, Now);
                    Assert.Equal(to, ticket.Status);
                }
                else
                {
                    Assert.Throws<DomainRuleViolationException>(() => ticket.ChangeStatus(to, Now));
                    Assert.Equal(from, ticket.Status);
                }
            }
        }
    }

    /// <summary>
    /// A closed ticket cannot skip back into the middle of the workflow.
    /// </summary>
    /// <remarks>
    /// Called out separately from the exhaustive sweep because it is the rule
    /// the product actually depends on: reopening goes through Open, where it is
    /// visible, rather than quietly reappearing as work in progress.
    /// </remarks>
    [Theory]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting)]
    [InlineData(TicketStatus.Resolved)]
    public void A_closed_ticket_can_only_be_reopened(TicketStatus target)
    {
        var ticket = TicketAt(TicketStatus.Closed);

        Assert.Throws<DomainRuleViolationException>(() => ticket.ChangeStatus(target, Now));

        ticket.ChangeStatus(TicketStatus.Open, Now);

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void Setting_the_status_it_already_has_is_not_an_error()
    {
        var ticket = NewTicket();

        ticket.ChangeStatus(TicketStatus.Open, Now);

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void An_undefined_status_is_rejected()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainRuleViolationException>(
            () => ticket.ChangeStatus((TicketStatus)42, Now));
    }

    [Fact]
    public void Resolving_records_when_the_work_was_first_considered_done()
    {
        var ticket = NewTicket();
        var firstResolution = Now.AddHours(2);

        ticket.ChangeStatus(TicketStatus.Resolved, firstResolution);

        Assert.Equal(firstResolution, ticket.ResolvedAt);
    }

    /// <summary>
    /// Reopening and resolving again keeps the original resolution time.
    /// </summary>
    /// <remarks>
    /// Overwriting it would erase how long the first attempt took, which is
    /// precisely the number a team looks at when a ticket comes back.
    /// </remarks>
    [Fact]
    public void Resolving_a_second_time_does_not_overwrite_the_first_resolution()
    {
        var ticket = NewTicket();
        var firstResolution = Now.AddHours(2);

        ticket.ChangeStatus(TicketStatus.Resolved, firstResolution);
        ticket.ChangeStatus(TicketStatus.Open, Now.AddDays(1));
        ticket.ChangeStatus(TicketStatus.Resolved, Now.AddDays(2));

        Assert.Equal(firstResolution, ticket.ResolvedAt);
    }

    [Fact]
    public void Available_transitions_exclude_the_current_status()
    {
        var ticket = TicketAt(TicketStatus.Closed);

        Assert.Equal([TicketStatus.Open], ticket.AvailableTransitions);
    }

    private static Ticket NewTicket() =>
        Ticket.Create(
            Guid.CreateVersion7(),
            TicketNumber.FirstNumber,
            Guid.CreateVersion7(),
            "Fatura PDF'i indirilemiyor",
            "Müşteri Mart ayı faturasını indirmeye çalışırken hata alıyor.",
            TicketPriority.Medium,
            Guid.CreateVersion7(),
            assignedUserId: null,
            Now);

    /// <summary>
    /// Builds a ticket already sitting at the given status, using only legal
    /// moves — so the fixture cannot manufacture a state the domain forbids.
    /// </summary>
    private static Ticket TicketAt(TicketStatus status)
    {
        var ticket = NewTicket();

        if (status is not TicketStatus.Open)
        {
            ticket.ChangeStatus(status, Now);
        }

        return ticket;
    }
}
