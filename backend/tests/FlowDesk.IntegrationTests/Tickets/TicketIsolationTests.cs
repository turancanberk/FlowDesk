using System.Net;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tickets;

/// <summary>
/// Tenant isolation for tickets and their comments.
/// </summary>
/// <remarks>
/// A security boundary, not a convenience check (docs/SECURITY.md). These tests
/// must never be deleted, skipped or weakened: a failure means one organisation
/// can read, change or attach records to another's support history.
///
/// Tickets reach further than customers do — they point at a customer and at a
/// person, and both references are somewhere a foreign id could be smuggled in
/// through a request body. That is what most of these cover.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TicketIsolationTests
{
    private readonly PostgresContainerFixture _postgres;

    public TicketIsolationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Listing_returns_only_the_current_workspaces_tickets()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var first = await TicketWorkspace.CreateAsync(factory, Cancellation);
        await TicketTestClient.CreateAndReadAsync(
            first.Client, first.Slug, first.CustomerId, Cancellation, "Birinci alanın talebi");

        using var second = await TicketWorkspace.CreateAsync(factory, Cancellation);
        await TicketTestClient.CreateAndReadAsync(
            second.Client, second.Slug, second.CustomerId, Cancellation, "İkinci alanın talebi");

        using var response = await TicketTestClient.ListAsync(
            second.Client, second.Slug, Cancellation);

        var page = await TicketTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(page.Items);
        Assert.Equal("İkinci alanın talebi", page.Items[0].Subject);

    }

    [Fact]
    public async Task A_stranger_cannot_read_another_workspaces_ticket()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await TicketWorkspace.CreateAsync(factory, Cancellation);
        var ticket = await TicketTestClient.CreateAndReadAsync(
            owner.Client, owner.Slug, owner.CustomerId, Cancellation);

        using var stranger = await TicketWorkspace.CreateAsync(factory, Cancellation);

        // Through the stranger's own workspace, using the other ticket's id.
        using var throughOwnWorkspace = await TicketTestClient.GetAsync(
            stranger.Client, stranger.Slug, ticket.Id, Cancellation);

        // And directly against the workspace they do not belong to.
        using var throughForeignWorkspace = await TicketTestClient.GetAsync(
            stranger.Client, owner.Slug, ticket.Id, Cancellation);

        // 404 in both cases, never 403: a 403 would confirm the ticket exists
        // (ADR-0007).
        Assert.Equal(HttpStatusCode.NotFound, throughOwnWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, throughForeignWorkspace.StatusCode);

    }

    [Fact]
    public async Task A_stranger_cannot_change_another_workspaces_ticket()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await TicketWorkspace.CreateAsync(factory, Cancellation);
        var ticket = await TicketTestClient.CreateAndReadAsync(
            owner.Client, owner.Slug, owner.CustomerId, Cancellation);

        using var stranger = await TicketWorkspace.CreateAsync(factory, Cancellation);

        using var status = await TicketTestClient.ChangeStatusAsync(
            stranger.Client, stranger.Slug, ticket.Id, TicketStatus.Closed,
            Cancellation);
        using var assignment = await TicketTestClient.AssignAsync(
            stranger.Client, stranger.Slug, ticket.Id, stranger.User.Id, Cancellation);
        using var deletion = await TicketTestClient.DeleteAsync(
            stranger.Client, stranger.Slug, ticket.Id, Cancellation);
        using var comment = await TicketTestClient.AddCommentAsync(
            stranger.Client, stranger.Slug, ticket.Id, "Buraya yazamamalıyım.", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, assignment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, comment.StatusCode);

        // And the ticket is untouched.
        using var reread = await TicketTestClient.GetAsync(
            owner.Client, owner.Slug, ticket.Id, Cancellation);
        var unchanged = await TicketTestClient.ReadDetailAsync(reread, Cancellation);

        Assert.Equal(TicketStatus.Open, unchanged.Status);
        Assert.Null(unchanged.AssignedUserId);

    }

    /// <summary>
    /// A ticket cannot be raised against a customer from another workspace.
    /// </summary>
    /// <remarks>
    /// The query filter scopes what a request can read; it does not stop a
    /// foreign id arriving in a request body. This is the write-side ownership
    /// check, and without it a ticket would quietly pull another organisation's
    /// customer into this workspace's data.
    /// </remarks>
    [Fact]
    public async Task A_ticket_cannot_be_raised_against_a_foreign_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var foreign = await TicketWorkspace.CreateAsync(factory, Cancellation);
        using var mine = await TicketWorkspace.CreateAsync(factory, Cancellation);

        using var response = await TicketTestClient.CreateAsync(
            mine.Client, mine.Slug, foreign.CustomerId, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "ticket.customer_not_found",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));

    }

    /// <summary>
    /// A ticket cannot be moved onto a customer from another workspace either.
    /// </summary>
    [Fact]
    public async Task A_ticket_cannot_be_moved_onto_a_foreign_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var foreign = await TicketWorkspace.CreateAsync(factory, Cancellation);
        using var mine = await TicketWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            mine.Client, mine.Slug, mine.CustomerId, Cancellation);

        using var response = await TicketTestClient.UpdateAsync(
            mine.Client, mine.Slug, ticket.Id, foreign.CustomerId, ticket.Version, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

    }

    /// <summary>
    /// A ticket cannot be assigned to someone outside the workspace.
    /// </summary>
    /// <remarks>
    /// Without this check a ticket could name any user id in the system as its
    /// owner, putting a stranger's name on a record they cannot see and cannot
    /// act on.
    /// </remarks>
    [Fact]
    public async Task A_ticket_cannot_be_assigned_to_a_non_member()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var mine = await TicketWorkspace.CreateAsync(factory, Cancellation);
        using var outsider = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var onCreate = await TicketTestClient.CreateAsync(
            mine.Client, mine.Slug, mine.CustomerId, Cancellation,
            assignedUserId: outsider.Id);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            mine.Client, mine.Slug, mine.CustomerId, Cancellation);

        using var onAssign = await TicketTestClient.AssignAsync(
            mine.Client, mine.Slug, ticket.Id, outsider.Id, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, onCreate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, onAssign.StatusCode);
        Assert.Equal(
            "ticket.assignee_not_a_member",
            await AuthTestClient.ReadProblemCodeAsync(onAssign, Cancellation));

    }

    /// <summary>
    /// A comment thread belongs to its workspace as much as the ticket does.
    /// </summary>
    [Fact]
    public async Task Comments_are_not_visible_across_workspaces()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await TicketWorkspace.CreateAsync(factory, Cancellation);
        var ticket = await TicketTestClient.CreateAndReadAsync(
            owner.Client, owner.Slug, owner.CustomerId, Cancellation);

        using var added = await TicketTestClient.AddCommentAsync(
            owner.Client, owner.Slug, ticket.Id, "Müşteriyle görüşüldü.", Cancellation);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        using var stranger = await TicketWorkspace.CreateAsync(factory, Cancellation);

        using var throughOwnWorkspace = await TicketTestClient.ListCommentsAsync(
            stranger.Client, stranger.Slug, ticket.Id, Cancellation);
        using var throughForeignWorkspace = await TicketTestClient.ListCommentsAsync(
            stranger.Client, owner.Slug, ticket.Id, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, throughOwnWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, throughForeignWorkspace.StatusCode);
    }
}
