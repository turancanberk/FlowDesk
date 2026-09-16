using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.UnitTests.Tickets;

/// <summary>Invariants a ticket holds regardless of its status.</summary>
public sealed class TicketTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_ticket_starts_open_and_unresolved()
    {
        var ticket = NewTicket();

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAt);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.Equal(Now, ticket.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_ticket_must_have_a_subject(string subject)
    {
        Assert.Throws<DomainRuleViolationException>(() => NewTicket(subject: subject));
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_from_the_subject()
    {
        var ticket = NewTicket(subject: "  Fatura indirilemiyor  ");

        Assert.Equal("Fatura indirilemiyor", ticket.Subject);
    }

    [Fact]
    public void A_subject_longer_than_the_column_is_rejected()
    {
        var tooLong = new string('a', Ticket.MaximumSubjectLength + 1);

        Assert.Throws<DomainRuleViolationException>(() => NewTicket(subject: tooLong));
    }

    [Fact]
    public void A_description_longer_than_the_column_is_rejected()
    {
        var tooLong = new string('a', Ticket.MaximumDescriptionLength + 1);

        Assert.Throws<DomainRuleViolationException>(() => NewTicket(description: tooLong));
    }

    /// <summary>
    /// A description is optional; a ticket raised from a phone call may be only
    /// a subject line.
    /// </summary>
    [Fact]
    public void A_ticket_may_have_no_description()
    {
        var ticket = NewTicket(description: "   ");

        Assert.Equal(string.Empty, ticket.Description);
    }

    [Fact]
    public void A_ticket_must_belong_to_a_workspace_and_a_customer()
    {
        Assert.Throws<DomainRuleViolationException>(() => NewTicket(tenantId: Guid.Empty));
        Assert.Throws<DomainRuleViolationException>(() => NewTicket(customerId: Guid.Empty));
        Assert.Throws<DomainRuleViolationException>(() => NewTicket(createdByUserId: Guid.Empty));
    }

    /// <summary>
    /// Numbering starts above zero so the first ticket does not read like a
    /// placeholder, and the entity refuses anything below that floor.
    /// </summary>
    [Fact]
    public void A_number_below_the_first_one_is_rejected()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => NewTicket(number: TicketNumber.FirstNumber - 1));
    }

    [Fact]
    public void Unassigning_uses_null_rather_than_an_empty_id()
    {
        var ticket = NewTicket();

        ticket.Assign(null, Now);
        Assert.Null(ticket.AssignedUserId);

        Assert.Throws<DomainRuleViolationException>(() => ticket.Assign(Guid.Empty, Now));
    }

    [Fact]
    public void Reassigning_to_no_customer_is_rejected()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainRuleViolationException>(() => ticket.ReassignCustomer(Guid.Empty, Now));
    }

    [Fact]
    public void An_undefined_priority_is_rejected()
    {
        var ticket = NewTicket();

        Assert.Throws<DomainRuleViolationException>(
            () => ticket.ChangePriority((TicketPriority)42, Now));
        Assert.Throws<DomainRuleViolationException>(
            () => NewTicket(priority: (TicketPriority)42));
    }

    [Fact]
    public void Every_change_moves_the_updated_timestamp()
    {
        var later = Now.AddHours(3);

        var updated = NewTicket();
        updated.UpdateDetails("Yeni konu", "Yeni açıklama", later);
        Assert.Equal(later, updated.UpdatedAt);

        var assigned = NewTicket();
        assigned.Assign(Guid.CreateVersion7(), later);
        Assert.Equal(later, assigned.UpdatedAt);

        var prioritised = NewTicket();
        prioritised.ChangePriority(TicketPriority.Urgent, later);
        Assert.Equal(later, prioritised.UpdatedAt);

        var moved = NewTicket();
        moved.ReassignCustomer(Guid.CreateVersion7(), later);
        Assert.Equal(later, moved.UpdatedAt);
    }

    private static Ticket NewTicket(
        Guid? tenantId = null,
        int number = TicketNumber.FirstNumber,
        Guid? customerId = null,
        string subject = "Fatura PDF'i indirilemiyor",
        string description = "Müşteri Mart ayı faturasını indiremiyor.",
        TicketPriority priority = TicketPriority.Medium,
        Guid? createdByUserId = null) =>
        Ticket.Create(
            tenantId ?? Guid.CreateVersion7(),
            number,
            customerId ?? Guid.CreateVersion7(),
            subject,
            description,
            priority,
            createdByUserId ?? Guid.CreateVersion7(),
            assignedUserId: null,
            Now);
}
