using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Dashboard;

/// <summary>
/// The operational figures a workspace's dashboard reports.
/// </summary>
/// <remarks>
/// The counts are the point of the screen, so they are asserted against known
/// data rather than checked for being non-zero. A dashboard that is merely
/// "populated" can still be wrong in the way that matters: reporting calm while
/// the queue fills up.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class DashboardTests
{
    private readonly PostgresContainerFixture _postgres;

    public DashboardTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_empty_workspace_reports_zeroes_and_one_member()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var user = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(user.Client, Cancellation);

        var dashboard = await ReadAsync(user.Client, workspace.Slug);

        Assert.Equal(0, dashboard.CustomerCount);
        Assert.Equal(0, dashboard.OpenTicketCount);
        Assert.Equal(0, dashboard.UnassignedTicketCount);
        Assert.Equal(0, dashboard.OverdueTaskCount);
        Assert.Equal(0, dashboard.DueSoonTaskCount);
        // The creator is a member of their own workspace.
        Assert.Equal(1, dashboard.MemberCount);
        Assert.Empty(dashboard.TicketsByStatus);
        Assert.Empty(dashboard.RecentTickets);
        Assert.Empty(dashboard.UpcomingTasks);
    }

    /// <summary>
    /// "Open" means unfinished, not the Open status alone.
    /// </summary>
    /// <remarks>
    /// A ticket in InProgress or Waiting is still someone's problem. A count
    /// that excluded them would read as calm while the queue filled up, which
    /// is the failure a dashboard exists to prevent.
    /// </remarks>
    [Fact]
    public async Task Open_tickets_count_everything_that_is_not_finished()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await CreateTicketAtAsync(workspace, "Açık", TicketStatus.Open);
        await CreateTicketAtAsync(workspace, "İşlemde", TicketStatus.InProgress);
        await CreateTicketAtAsync(workspace, "Bekliyor", TicketStatus.Waiting);
        await CreateTicketAtAsync(workspace, "Çözüldü", TicketStatus.Resolved);
        await CreateTicketAtAsync(workspace, "Kapalı", TicketStatus.Closed);

        var dashboard = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(3, dashboard.OpenTicketCount);

        // Every status is reported separately as well, ordered by the enum so
        // the chart's bars keep their places.
        Assert.Equal(
            [
                TicketStatus.Open,
                TicketStatus.InProgress,
                TicketStatus.Waiting,
                TicketStatus.Resolved,
                TicketStatus.Closed,
            ],
            dashboard.TicketsByStatus.Select(entry => entry.Status));
        Assert.All(dashboard.TicketsByStatus, entry => Assert.Equal(1, entry.Count));
    }

    /// <summary>
    /// The unassigned count covers work waiting to be picked up, not work
    /// already finished without an owner.
    /// </summary>
    [Fact]
    public async Task Unassigned_tickets_exclude_finished_ones()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await CreateTicketAtAsync(workspace, "Kimsede değil", TicketStatus.Open);
        await CreateTicketAtAsync(workspace, "Kapatılmış", TicketStatus.Closed);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var assigned = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "Atanmış",
            assignedUserId: agent.Id);
        Assert.NotNull(assigned.AssignedUserId);

        var dashboard = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(1, dashboard.UnassignedTicketCount);
        Assert.Equal(2, dashboard.MemberCount);
    }

    /// <summary>
    /// Overdue and due-soon split the dated work, and finished work is in
    /// neither.
    /// </summary>
    [Fact]
    public async Task Task_counts_separate_overdue_from_due_soon()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Geciken",
            dueAt: DateTimeOffset.UtcNow.AddDays(-2));

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Bu hafta",
            dueAt: DateTimeOffset.UtcNow.AddDays(3));

        // Beyond the week the dashboard looks ahead over.
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Gelecek ay",
            dueAt: DateTimeOffset.UtcNow.AddDays(30));

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Tarihsiz");

        var finishedLate = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Geç biten",
            dueAt: DateTimeOffset.UtcNow.AddDays(-5));

        using var completed = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, finishedLate.Id, TaskItemStatus.Done, Cancellation);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        var dashboard = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(1, dashboard.OverdueTaskCount);
        Assert.Equal(1, dashboard.DueSoonTaskCount);
    }

    /// <summary>
    /// The upcoming list leads with what is already late.
    /// </summary>
    /// <remarks>
    /// A list of only future deadlines would quietly drop the things that are
    /// already overdue, which are the ones that need attention most.
    /// </remarks>
    [Fact]
    public async Task Upcoming_tasks_lead_with_overdue_work()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Yarın",
            dueAt: DateTimeOffset.UtcNow.AddDays(1));
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Geçen hafta",
            dueAt: DateTimeOffset.UtcNow.AddDays(-7));
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Tarihsiz");

        var dashboard = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(
            ["Geçen hafta", "Yarın"],
            dashboard.UpcomingTasks.Select(task => task.Title));
        Assert.True(dashboard.UpcomingTasks[0].IsOverdue);
        Assert.False(dashboard.UpcomingTasks[1].IsOverdue);
    }

    [Fact]
    public async Task Recent_tickets_are_the_most_recently_touched()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var first = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "İlk açılan");
        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "Sonra açılan");

        // Touching the older ticket puts it back on top.
        using var moved = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, first.Id, TicketStatus.InProgress, Cancellation);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var dashboard = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal("İlk açılan", dashboard.RecentTickets[0].Subject);
        Assert.Equal("Acme Teknoloji", dashboard.RecentTickets[0].CustomerName);
        Assert.Equal(TicketStatus.InProgress, dashboard.RecentTickets[0].Status);
    }

    /// <summary>
    /// The dashboard reports this workspace and no other.
    /// </summary>
    /// <remarks>
    /// A security boundary, not a convenience check (docs/SECURITY.md). Member
    /// count is the figure most at risk here: memberships sit outside the global
    /// query filter on purpose (ADR-0024), so it is the one count whose
    /// workspace condition is written by hand and could be left out.
    /// </remarks>
    [Fact]
    public async Task The_figures_never_include_another_workspace()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var busy = await TestWorkspace.CreateAsync(factory, Cancellation);
        await TeamTestClient.AddMemberAsync(
            factory, busy.Client, busy.Slug, MembershipRole.Agent, Cancellation);
        await TicketTestClient.CreateAndReadAsync(
            busy.Client, busy.Slug, busy.CustomerId, Cancellation);
        await TaskTestClient.CreateAndReadAsync(
            busy.Client, busy.Slug, Cancellation, dueAt: DateTimeOffset.UtcNow.AddDays(-1));

        using var quiet = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var quietWorkspace = await WorkspaceTestClient.CreateOwnedAsync(quiet.Client, Cancellation);

        var dashboard = await ReadAsync(quiet.Client, quietWorkspace.Slug);

        Assert.Equal(0, dashboard.CustomerCount);
        Assert.Equal(0, dashboard.OpenTicketCount);
        Assert.Equal(0, dashboard.OverdueTaskCount);
        Assert.Equal(1, dashboard.MemberCount);
        Assert.Empty(dashboard.RecentTickets);
        Assert.Empty(dashboard.UpcomingTasks);
    }

    [Fact]
    public async Task A_stranger_cannot_read_a_workspaces_dashboard()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var response = await stranger.Client.GetAsync(
            new Uri($"/api/workspaces/{owner.Slug}/dashboard", UriKind.Relative), Cancellation);

        // 404, not 403: a 403 would confirm the workspace exists (ADR-0007).
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A Viewer sees the dashboard; reading it is not an admin act.</summary>
    [Fact]
    public async Task A_viewer_can_read_the_dashboard()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Viewer, Cancellation);

        using var response = await viewer.Client.GetAsync(
            new Uri($"/api/workspaces/{workspace.Slug}/dashboard", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<DashboardResponse> ReadAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync(
            new Uri($"/api/workspaces/{slug}/dashboard", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(
            FlowDeskJson.Options, Cancellation);

        Assert.NotNull(dashboard);

        return dashboard;
    }

    /// <summary>Creates a ticket and moves it to the given status.</summary>
    private static async Task CreateTicketAtAsync(
        TestWorkspace workspace,
        string subject,
        TicketStatus status)
    {
        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, subject);

        if (status is TicketStatus.Open)
        {
            return;
        }

        using var response = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, ticket.Id, status, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
