using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Activity;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Activity;

/// <summary>
/// The workspace's history.
/// </summary>
/// <remarks>
/// Driven through the API, because what is being checked is that the record is
/// written by the ordinary act of using the product — not by a test calling a
/// recorder directly. A history that only exists when something remembers to
/// write it is the failure this phase exists to avoid (ADR-0036).
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class ActivityTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly AzuriteContainerFixture _storage;

    public ActivityTests(PostgresContainerFixture postgres, AzuriteContainerFixture storage)
    {
        _postgres = postgres;
        _storage = storage;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private FlowDeskApiFactory CreateFactory() =>
        new(_postgres.ConnectionString, storageConnectionString: _storage.ConnectionString);

    [Fact]
    public async Task Creating_a_customer_is_recorded_with_its_name()
    {
        await using var factory = CreateFactory();
        using var user = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(user.Client, Cancellation);

        var customer = await CustomerTestClient.CreateAndReadAsync(
            user.Client, workspace.Slug, "Kuzey Yazılım", Cancellation);

        var feed = await ReadAsync(user.Client, workspace.Slug);
        var entry = Assert.Single(feed.Items);

        Assert.Equal(ActivityType.CustomerCreated, entry.Type);
        Assert.Equal(ActivitySubject.Customer, entry.SubjectType);
        Assert.Equal(customer.Id, entry.SubjectId);
        Assert.Equal(user.Id, entry.ActorUserId);
        Assert.Equal("Kuzey Yazılım", entry.Payload.GetProperty("name").GetString());
    }

    /// <summary>
    /// A ticket's whole life leaves a trail, newest first.
    /// </summary>
    [Fact]
    public async Task A_tickets_life_is_recorded_newest_first()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var assigned = await TicketTestClient.AssignAsync(
            workspace.Client, workspace.Slug, ticket.Id, agent.Id, Cancellation);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        using var moved = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.InProgress, Cancellation);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        using var commented = await TicketTestClient.AddCommentAsync(
            workspace.Client, workspace.Slug, ticket.Id, "Müşteriyle görüşüldü.", Cancellation);
        Assert.Equal(HttpStatusCode.Created, commented.StatusCode);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"subjectType={ActivitySubject.Ticket}");

        Assert.Equal(
            [
                ActivityType.TicketCommented,
                ActivityType.TicketStatusChanged,
                ActivityType.TicketAssigned,
                ActivityType.TicketCreated,
            ],
            feed.Items.Select(item => item.Type));
    }

    /// <summary>
    /// A status change records where it came from as well as where it went.
    /// </summary>
    [Fact]
    public async Task A_status_change_records_both_ends()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var moved = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Resolved, Cancellation);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"type={ActivityType.TicketStatusChanged}");
        var entry = Assert.Single(feed.Items);

        Assert.Equal("Open", entry.Payload.GetProperty("from").GetString());
        Assert.Equal("Resolved", entry.Payload.GetProperty("to").GetString());
    }

    /// <summary>
    /// Setting the status a ticket already has records nothing.
    /// </summary>
    /// <remarks>
    /// A no-op in the domain should be a no-op in the history. Recording it
    /// would fill the feed with lines saying nothing changed.
    /// </remarks>
    [Fact]
    public async Task A_status_change_that_changes_nothing_is_not_recorded()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        using var same = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, TicketStatus.Open, Cancellation);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"type={ActivityType.TicketStatusChanged}");

        Assert.Empty(feed.Items);
    }

    /// <summary>
    /// A deleted ticket keeps its line, with the number and subject in it.
    /// </summary>
    /// <remarks>
    /// This is why an activity event holds no foreign key to its subject: a
    /// cascade would delete the record of the deletion.
    /// </remarks>
    [Fact]
    public async Task A_deleted_ticket_keeps_its_place_in_the_history()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Silinecek talep");

        using var deleted = await TicketTestClient.DeleteAsync(
            workspace.Client, workspace.Slug, ticket.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"type={ActivityType.TicketDeleted}");
        var entry = Assert.Single(feed.Items);

        Assert.Equal(ticket.Id, entry.SubjectId);
        Assert.Equal("Silinecek talep", entry.Payload.GetProperty("subject").GetString());
        Assert.Equal(ticket.Number, entry.Payload.GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task Completing_a_task_is_recorded_but_starting_one_is_not()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Sözleşmeyi gözden geçir");

        using var started = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, task.Id, TaskItemStatus.InProgress, Cancellation);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        using var done = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, task.Id, TaskItemStatus.Done, Cancellation);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"subjectType={ActivitySubject.TaskItem}");

        Assert.Equal(
            [ActivityType.TaskCompleted, ActivityType.TaskCreated],
            feed.Items.Select(item => item.Type));
    }

    /// <summary>
    /// An invitation's line names the address and the role, and nothing else.
    /// </summary>
    /// <remarks>
    /// The token is deliberately absent. It travels in the message that sends
    /// the e-mail, where it has to; an audit trail is read by more people and
    /// kept for longer, and a usable invitation link sitting in it would be a
    /// standing way in (docs/SECURITY.md).
    /// </remarks>
    [Fact]
    public async Task An_invitations_record_carries_no_token()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var created = await TeamTestClient.InviteAndReadAsync(
            workspace.Client, workspace.Slug, "davetli@ornek.test",
            MembershipRole.Agent, Cancellation);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"type={ActivityType.MemberInvited}");
        var entry = Assert.Single(feed.Items);

        Assert.Equal("davetli@ornek.test", entry.Payload.GetProperty("email").GetString());
        Assert.Equal("Agent", entry.Payload.GetProperty("role").GetString());

        var raw = entry.Payload.GetRawText();

        Assert.DoesNotContain(created.Token, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Accepting an invitation is recorded against the workspace joined.
    /// </summary>
    /// <remarks>
    /// The one flow where the workspace is named explicitly: the person is not a
    /// member until that moment, so there is none to resolve.
    /// </remarks>
    [Fact]
    public async Task Joining_a_workspace_is_recorded_against_it()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var joiner = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var feed = await ReadAsync(
            workspace.Client, workspace.Slug, $"type={ActivityType.MemberJoined}");
        var entry = Assert.Single(feed.Items);

        Assert.Equal(joiner.Id, entry.ActorUserId);
        Assert.Equal(joiner.Id, entry.SubjectId);
    }

    /// <summary>
    /// The history stops at the workspace boundary.
    /// </summary>
    /// <remarks>
    /// A security boundary, not a convenience check (docs/SECURITY.md).
    /// </remarks>
    [Fact]
    public async Task The_history_never_includes_another_workspace()
    {
        await using var factory = CreateFactory();

        using var busy = await TestWorkspace.CreateAsync(factory, Cancellation);
        await TicketTestClient.CreateAndReadAsync(
            busy.Client, busy.Slug, busy.CustomerId, Cancellation);

        using var quiet = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var quietWorkspace = await WorkspaceTestClient.CreateOwnedAsync(quiet.Client, Cancellation);

        var feed = await ReadAsync(quiet.Client, quietWorkspace.Slug);

        Assert.Empty(feed.Items);
        Assert.Equal(0, feed.TotalCount);
    }

    [Fact]
    public async Task A_stranger_cannot_read_a_workspaces_history()
    {
        await using var factory = CreateFactory();

        using var owner = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var response = await stranger.Client.GetAsync(
            new Uri($"/api/workspaces/{owner.Slug}/activity", UriKind.Relative), Cancellation);

        // 404, not 403: a 403 would confirm the workspace exists (ADR-0007).
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A Viewer reads the history.
    /// </summary>
    /// <remarks>
    /// The feed says what the team did, and hiding a team's own history from
    /// part of that team would make the record less trustworthy without making
    /// anything safer (ADR-0036).
    /// </remarks>
    [Fact]
    public async Task A_viewer_can_read_the_history()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Viewer, Cancellation);

        using var response = await viewer.Client.GetAsync(
            new Uri($"/api/workspaces/{workspace.Slug}/activity", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// A record's own history is readable on its own.
    /// </summary>
    [Fact]
    public async Task One_records_history_can_be_read_on_its_own()
    {
        await using var factory = CreateFactory();
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var first = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "Birinci");
        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "İkinci");

        var feed = await ReadAsync(
            workspace.Client,
            workspace.Slug,
            $"subjectType={ActivitySubject.Ticket}&subjectId={first.Id}");

        var entry = Assert.Single(feed.Items);

        Assert.Equal(first.Id, entry.SubjectId);
        Assert.Equal("Birinci", entry.Payload.GetProperty("subject").GetString());
    }

    private static async Task<PagedResponse<ActivityResponse>> ReadAsync(
        HttpClient client,
        string slug,
        string? query = null)
    {
        using var response = await client.GetAsync(
            new Uri(
                $"/api/workspaces/{slug}/activity{(query is null ? string.Empty : $"?{query}")}",
                UriKind.Relative),
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var feed = await response.Content.ReadFromJsonAsync<PagedResponse<ActivityResponse>>(
            FlowDeskJson.Options, Cancellation);

        Assert.NotNull(feed);

        return feed;
    }
}
