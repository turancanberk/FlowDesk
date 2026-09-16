using System.Net;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tickets;

/// <summary>
/// Per-workspace ticket numbering, including under simultaneous creation.
/// </summary>
/// <remarks>
/// The number is what a team and a customer quote to each other, so two tickets
/// sharing one would make "TLP-1042" ambiguous in exactly the conversation the
/// number exists to make possible.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TicketNumberingTests
{
    private readonly PostgresContainerFixture _postgres;

    public TicketNumberingTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Numbers_run_in_sequence_within_a_workspace()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TicketWorkspace.CreateAsync(factory, Cancellation);

        var numbers = new List<int>();

        for (var index = 0; index < 4; index++)
        {
            var ticket = await TicketTestClient.CreateAndReadAsync(
                workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
                $"Talep {index}");

            numbers.Add(ticket.Number);
        }

        Assert.Equal(
            [
                TicketNumber.FirstNumber,
                TicketNumber.FirstNumber + 1,
                TicketNumber.FirstNumber + 2,
                TicketNumber.FirstNumber + 3,
            ],
            numbers);
    }

    /// <summary>
    /// Each workspace counts from the start, independently of the others.
    /// </summary>
    /// <remarks>
    /// Numbering globally would let one organisation infer another's volume from
    /// the gaps in its own sequence (docs/SECURITY.md).
    /// </remarks>
    [Fact]
    public async Task Each_workspace_starts_its_own_sequence()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var first = await TicketWorkspace.CreateAsync(factory, Cancellation);
        using var second = await TicketWorkspace.CreateAsync(factory, Cancellation);

        var firstTicket = await TicketTestClient.CreateAndReadAsync(
            first.Client, first.Slug, first.CustomerId, Cancellation);
        await TicketTestClient.CreateAndReadAsync(
            first.Client, first.Slug, first.CustomerId, Cancellation);

        var secondTicket = await TicketTestClient.CreateAndReadAsync(
            second.Client, second.Slug, second.CustomerId, Cancellation);

        Assert.Equal(TicketNumber.FirstNumber, firstTicket.Number);
        Assert.Equal(TicketNumber.FirstNumber, secondTicket.Number);
    }

    /// <summary>
    /// Ten simultaneous creations produce ten distinct, gapless numbers.
    /// </summary>
    /// <remarks>
    /// This is the test the counter design exists for. Without the row lock, two
    /// requests would read the same next number and one of them would either
    /// duplicate it or be rejected by the unique index. Run against a real
    /// PostgreSQL, because an in-memory provider would not take the lock and the
    /// test would pass while proving nothing.
    /// </remarks>
    [Fact]
    public async Task Simultaneous_creations_never_share_a_number()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TicketWorkspace.CreateAsync(factory, Cancellation);

        const int simultaneousCreations = 10;

        var responses = await Task.WhenAll(
            Enumerable.Range(0, simultaneousCreations).Select(index =>
                TicketTestClient.CreateAsync(
                    workspace.Client,
                    workspace.Slug,
                    workspace.CustomerId,
                    Cancellation,
                    $"Eşzamanlı talep {index}")));

        var numbers = new List<int>();

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var ticket = await TicketTestClient.ReadDetailAsync(response, Cancellation);
            numbers.Add(ticket.Number);

            response.Dispose();
        }

        Assert.Equal(simultaneousCreations, numbers.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(TicketNumber.FirstNumber, simultaneousCreations),
            numbers.Order());
    }
}
