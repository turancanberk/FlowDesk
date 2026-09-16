using System.Net;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tickets;

/// <summary>
/// What each role may do with a ticket, verified through the API.
/// </summary>
/// <remarks>
/// The matrix itself is asserted as a unit test. What matters here is that
/// every ticket endpoint actually consults it — a handler that forgot the check
/// would pass the unit test and still let a Viewer close tickets.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TicketAuthorizationTests
{
    private readonly PostgresContainerFixture _postgres;

    public TicketAuthorizationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// A Viewer reads tickets but changes nothing.
    /// </summary>
    [Fact]
    public async Task A_viewer_can_read_but_not_change_a_ticket()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TicketWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Viewer, Cancellation);

        using var read = await TicketTestClient.GetAsync(
            viewer.Client, workspace.Slug, ticket.Id, Cancellation);
        using var listed = await TicketTestClient.ListAsync(
            viewer.Client, workspace.Slug, Cancellation);

        using var created = await TicketTestClient.CreateAsync(
            viewer.Client, workspace.Slug, workspace.CustomerId, Cancellation);
        using var edited = await TicketTestClient.UpdateAsync(
            viewer.Client, workspace.Slug, ticket.Id, workspace.CustomerId, ticket.Version,
            Cancellation);
        using var moved = await TicketTestClient.ChangeStatusAsync(
            viewer.Client, workspace.Slug, ticket.Id, TicketStatus.Closed, Cancellation);
        using var assigned = await TicketTestClient.AssignAsync(
            viewer.Client, workspace.Slug, ticket.Id, viewer.Id, Cancellation);
        using var commented = await TicketTestClient.AddCommentAsync(
            viewer.Client, workspace.Slug, ticket.Id, "Yorum yazamamalıyım.", Cancellation);
        using var deleted = await TicketTestClient.DeleteAsync(
            viewer.Client, workspace.Slug, ticket.Id, Cancellation);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        // 403, not 404: they are a member of this workspace, so the record's
        // existence is not a secret from them — only the action is refused
        // (docs/API_CONVENTIONS.md).
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edited.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, moved.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, commented.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);
    }

    /// <summary>
    /// An Agent does the support work but does not destroy records.
    /// </summary>
    [Fact]
    public async Task An_agent_can_work_a_ticket_but_not_delete_it()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TicketWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            agent.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var moved = await TicketTestClient.ChangeStatusAsync(
            agent.Client, workspace.Slug, ticket.Id, TicketStatus.InProgress, Cancellation);
        using var assigned = await TicketTestClient.AssignAsync(
            agent.Client, workspace.Slug, ticket.Id, agent.Id, Cancellation);
        using var commented = await TicketTestClient.AddCommentAsync(
            agent.Client, workspace.Slug, ticket.Id, "Üzerinde çalışıyorum.", Cancellation);
        using var deleted = await TicketTestClient.DeleteAsync(
            agent.Client, workspace.Slug, ticket.Id, Cancellation);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal(HttpStatusCode.Created, commented.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);
    }

    [Fact]
    public async Task An_admin_can_delete_a_ticket()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TicketWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var admin = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Admin, Cancellation);

        using var deleted = await TicketTestClient.DeleteAsync(
            admin.Client, workspace.Slug, ticket.Id, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_reaches_nothing()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TicketWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var anonymous = factory.CreateApiClient();

        using var listed = await TicketTestClient.ListAsync(anonymous, workspace.Slug, Cancellation);
        using var read = await TicketTestClient.GetAsync(
            anonymous, workspace.Slug, ticket.Id, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }
}
