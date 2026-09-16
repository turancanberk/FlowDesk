using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.UnitTests.Tickets;

public sealed class TicketNumberingTests
{
    [Fact]
    public void A_number_renders_with_the_prefix_a_team_would_quote()
    {
        Assert.Equal("TLP-1042", new TicketNumber(1042).ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TicketNumber.FirstNumber - 1)]
    public void A_number_below_the_floor_is_rejected(int value)
    {
        Assert.Throws<DomainRuleViolationException>(() => new TicketNumber(value));
    }

    [Fact]
    public void A_counter_starts_at_the_first_number()
    {
        var counter = TenantCounter.StartFor(Guid.CreateVersion7());

        Assert.Equal(TicketNumber.FirstNumber, counter.NextTicketNumber);
    }

    /// <summary>
    /// Taking a number hands out the current value and moves the counter on, so
    /// the same number is never handed out twice.
    /// </summary>
    [Fact]
    public void Taking_a_number_advances_the_counter()
    {
        var counter = TenantCounter.StartFor(Guid.CreateVersion7());

        var taken = Enumerable.Range(0, 5).Select(_ => counter.TakeNextTicketNumber()).ToArray();

        Assert.Equal(
            [
                TicketNumber.FirstNumber,
                TicketNumber.FirstNumber + 1,
                TicketNumber.FirstNumber + 2,
                TicketNumber.FirstNumber + 3,
                TicketNumber.FirstNumber + 4,
            ],
            taken);
        Assert.Equal(TicketNumber.FirstNumber + 5, counter.NextTicketNumber);
    }

    [Fact]
    public void A_counter_must_belong_to_a_workspace()
    {
        Assert.Throws<DomainRuleViolationException>(() => TenantCounter.StartFor(Guid.Empty));
    }
}
