using System.Net;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tasks;

/// <summary>
/// What each role may do with a task, verified through the API.
/// </summary>
/// <remarks>
/// The matrix is asserted as a unit test. What matters here is that every task
/// endpoint actually consults it — a handler that forgot the check would pass
/// the unit test and still let a Viewer tick work off the list.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TaskAuthorizationTests
{
    private readonly PostgresContainerFixture _postgres;

    public TaskAuthorizationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_viewer_can_read_but_not_change_a_task()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Viewer, Cancellation);

        using var read = await TaskTestClient.GetAsync(
            viewer.Client, workspace.Slug, task.Id, Cancellation);
        using var listed = await TaskTestClient.ListAsync(viewer.Client, workspace.Slug, Cancellation);
        using var created = await TaskTestClient.CreateAsync(
            viewer.Client, workspace.Slug, Cancellation);
        using var edited = await TaskTestClient.UpdateAsync(
            viewer.Client, workspace.Slug, task.Id, Cancellation);
        using var moved = await TaskTestClient.ChangeStatusAsync(
            viewer.Client, workspace.Slug, task.Id, TaskItemStatus.Done, Cancellation);
        using var deleted = await TaskTestClient.DeleteAsync(
            viewer.Client, workspace.Slug, task.Id, Cancellation);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        // 403, not 404: they belong to this workspace, so the record's existence
        // is not a secret from them — only the action is refused
        // (docs/API_CONVENTIONS.md).
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edited.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, moved.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);
    }

    /// <summary>
    /// An agent may delete a task, unlike a ticket.
    /// </summary>
    /// <remarks>
    /// A task is internal housekeeping: no customer conversation hangs off it,
    /// and nothing outside the team refers to it.
    /// </remarks>
    [Fact]
    public async Task An_agent_owns_their_tasks_including_deleting_them()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            agent.Client, workspace.Slug, Cancellation);

        using var moved = await TaskTestClient.ChangeStatusAsync(
            agent.Client, workspace.Slug, task.Id, TaskItemStatus.InProgress, Cancellation);
        using var deleted = await TaskTestClient.DeleteAsync(
            agent.Client, workspace.Slug, task.Id, Cancellation);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_reaches_nothing()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation);

        using var anonymous = factory.CreateApiClient();

        using var listed = await TaskTestClient.ListAsync(anonymous, workspace.Slug, Cancellation);
        using var read = await TaskTestClient.GetAsync(
            anonymous, workspace.Slug, task.Id, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }
}
