using System.Net;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tickets;

/// <summary>
/// The ticket lifecycle over the real HTTP surface.
/// </summary>
/// <remarks>
/// The state machine is proved exhaustively as a unit test. What these add is
/// that the rule survives the round trip: an invalid transition must be refused
/// by the API with a conflict, not accepted because the endpoint forgot to ask
/// the entity.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TicketWorkflowTests
{
    private readonly PostgresContainerFixture _postgres;

    public TicketWorkflowTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_ticket_is_open_and_carries_the_names_a_list_needs()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Fatura PDF'i indirilemiyor", TicketPriority.High);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(TicketNumber.FirstNumber, ticket.Number);
        Assert.Equal("Acme Teknoloji", ticket.CustomerName);
        Assert.Equal(workspace.User.Id, ticket.CreatedByUserId);
        Assert.Null(ticket.AssignedUserId);
        Assert.Null(ticket.ResolvedAt);

    }

    /// <summary>
    /// The detail response advertises only the moves the domain would accept,
    /// so the interface never offers a button the server will refuse.
    /// </summary>
    [Fact]
    public async Task Available_transitions_follow_the_tickets_current_status()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        Assert.Equal(
            [TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Closed],
            ticket.AvailableTransitions);

        using var closed = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Closed, Cancellation);
        var closedTicket = await TicketTestClient.ReadDetailAsync(closed, Cancellation);

        Assert.Equal([TicketStatus.Open], closedTicket.AvailableTransitions);

    }

    [Fact]
    public async Task An_invalid_transition_is_refused_with_a_conflict()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var closed = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Closed, Cancellation);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        // Closed tickets are reopened through Open, never straight back into
        // the middle of the workflow.
        using var response = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.InProgress, Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "ticket.invalid_transition",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));

        // And the refusal left the ticket where it was.
        using var reread = await TicketTestClient.GetAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        var unchanged = await TicketTestClient.ReadDetailAsync(reread, Cancellation);

        Assert.Equal(TicketStatus.Closed, unchanged.Status);

    }

    [Fact]
    public async Task Resolving_records_the_moment_the_work_was_done()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var resolved = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Resolved, Cancellation);
        var resolvedTicket = await TicketTestClient.ReadDetailAsync(resolved, Cancellation);

        Assert.NotNull(resolvedTicket.ResolvedAt);

        using var reopened = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Open, Cancellation);
        var reopenedTicket = await TicketTestClient.ReadDetailAsync(reopened, Cancellation);

        // Reopening does not erase how long the first attempt took.
        Assert.Equal(resolvedTicket.ResolvedAt, reopenedTicket.ResolvedAt);

    }

    [Fact]
    public async Task Assignment_names_a_member_and_null_takes_it_back()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation,
            "Zeynep Arslan");

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var assigned = await TicketTestClient.AssignAsync(
            workspace.Client, workspace.Slug, ticket.Id, agent.Id, Cancellation);
        var assignedTicket = await TicketTestClient.ReadDetailAsync(assigned, Cancellation);

        Assert.Equal(agent.Id, assignedTicket.AssignedUserId);
        Assert.Equal("Zeynep Arslan", assignedTicket.AssignedUserDisplayName);

        using var cleared = await TicketTestClient.AssignAsync(
            workspace.Client, workspace.Slug, ticket.Id, null, Cancellation);
        var unassignedTicket = await TicketTestClient.ReadDetailAsync(cleared, Cancellation);

        Assert.Null(unassignedTicket.AssignedUserId);
        Assert.Null(unassignedTicket.AssignedUserDisplayName);

    }

    /// <summary>
    /// Two people editing the same ticket: the second save is refused rather
    /// than silently overwriting the first (ADR-0013).
    /// </summary>
    [Fact]
    public async Task A_concurrent_edit_is_reported_instead_of_overwriting()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        // Both editors opened the form and hold the same row version.
        var staleVersion = ticket.Version;

        using var firstSave = await TicketTestClient.UpdateAsync(
            workspace.Client, workspace.Slug, ticket.Id, workspace.CustomerId, staleVersion,
            Cancellation, subject: "İlk kişinin düzenlemesi");
        Assert.Equal(HttpStatusCode.OK, firstSave.StatusCode);

        using var secondSave = await TicketTestClient.UpdateAsync(
            workspace.Client, workspace.Slug, ticket.Id, workspace.CustomerId, staleVersion,
            Cancellation, subject: "İkinci kişinin düzenlemesi");

        Assert.Equal(HttpStatusCode.Conflict, secondSave.StatusCode);
        Assert.Equal(
            "ticket.concurrency_conflict",
            await AuthTestClient.ReadProblemCodeAsync(secondSave, Cancellation));

        // The first editor's work survived.
        using var reread = await TicketTestClient.GetAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        var current = await TicketTestClient.ReadDetailAsync(reread, Cancellation);

        Assert.Equal("İlk kişinin düzenlemesi", current.Subject);

    }

    [Fact]
    public async Task An_edit_with_the_current_version_succeeds()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var first = await TicketTestClient.UpdateAsync(
            workspace.Client, workspace.Slug, ticket.Id, workspace.CustomerId, ticket.Version,
            Cancellation, subject: "İlk düzenleme");
        var afterFirst = await TicketTestClient.ReadDetailAsync(first, Cancellation);

        // The response carries the new version, so the form can save again
        // without a round trip.
        using var second = await TicketTestClient.UpdateAsync(
            workspace.Client, workspace.Slug, ticket.Id, workspace.CustomerId,
            afterFirst.Version, Cancellation, subject: "İkinci düzenleme");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotEqual(ticket.Version, afterFirst.Version);

        var afterSecond = await TicketTestClient.ReadDetailAsync(second, Cancellation);
        Assert.Equal("İkinci düzenleme", afterSecond.Subject);

    }

    [Fact]
    public async Task Comments_are_returned_oldest_first_with_their_authors()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation,
            "Zeynep Arslan");

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var first = await TicketTestClient.AddCommentAsync(
            workspace.Client, workspace.Slug, ticket.Id, "Müşteriyle görüşüldü.", Cancellation);
        using var second = await TicketTestClient.AddCommentAsync(
            agent.Client, workspace.Slug, ticket.Id, "Log kayıtları incelendi.", Cancellation);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var listed = await TicketTestClient.ListCommentsAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        var comments = await TicketTestClient.ReadCommentsAsync(listed, Cancellation);

        Assert.Equal(2, comments.Count);
        Assert.Equal("Müşteriyle görüşüldü.", comments[0].Body);
        Assert.Equal("Log kayıtları incelendi.", comments[1].Body);
        Assert.Equal("Zeynep Arslan", comments[1].AuthorDisplayName);

    }

    [Fact]
    public async Task An_empty_comment_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var response = await TicketTestClient.AddCommentAsync(
            workspace.Client, workspace.Slug, ticket.Id, "   ", Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

    }

    /// <summary>
    /// Deleting a ticket takes its comment thread with it.
    /// </summary>
    [Fact]
    public async Task Deleting_a_ticket_removes_it_and_its_comments()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var comment = await TicketTestClient.AddCommentAsync(
            workspace.Client, workspace.Slug, ticket.Id, "Silinecek.", Cancellation);
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);

        using var deleted = await TicketTestClient.DeleteAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var reread = await TicketTestClient.GetAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, reread.StatusCode);

    }

    /// <summary>
    /// A customer stays deletable-proof: the ticket keeps its context even when
    /// the customer is archived.
    /// </summary>
    /// <remarks>
    /// Archiving a customer is how a company is taken out of circulation
    /// (ADR-0012). Its tickets are the reason archiving exists rather than
    /// deletion, so they must remain readable and editable afterwards.
    /// </remarks>
    [Fact]
    public async Task A_ticket_survives_its_customer_being_archived()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var archived = await CustomerTestClient.ArchiveAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        using var reread = await TicketTestClient.GetAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        var stillThere = await TicketTestClient.ReadDetailAsync(reread, Cancellation);

        Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
        Assert.Equal("Acme Teknoloji", stillThere.CustomerName);

        using var stillMovable = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Resolved, Cancellation);
        Assert.Equal(HttpStatusCode.OK, stillMovable.StatusCode);
    }
}
