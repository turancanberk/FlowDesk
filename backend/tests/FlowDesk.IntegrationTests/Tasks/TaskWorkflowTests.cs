using System.Net;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tasks;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tasks;

/// <summary>The task lifecycle over the real HTTP surface.</summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class TaskWorkflowTests
{
    private readonly PostgresContainerFixture _postgres;

    public TaskWorkflowTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_task_is_todo_with_nothing_scheduled_or_assigned()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Sözleşmeyi gözden geçir");

        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Null(task.DueAt);
        Assert.Null(task.AssignedUserId);
        Assert.Null(task.CustomerId);
        Assert.Null(task.CompletedAt);
        Assert.False(task.IsOverdue);
        Assert.Equal(workspace.User.Id, task.CreatedByUserId);
    }

    [Fact]
    public async Task A_task_carries_the_customer_and_assignee_names_a_list_needs()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation,
            "Zeynep Arslan");

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation,
            customerId: workspace.CustomerId, assignedUserId: agent.Id);

        Assert.Equal("Acme Teknoloji", task.CustomerName);
        Assert.Equal("Zeynep Arslan", task.AssignedUserDisplayName);
    }

    /// <summary>
    /// A task has no state machine: any status may follow any other.
    /// </summary>
    [Fact]
    public async Task Any_status_may_follow_any_other()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation);

        foreach (var status in new[]
        {
            TaskItemStatus.Done,
            TaskItemStatus.Todo,
            TaskItemStatus.InProgress,
            TaskItemStatus.Done,
        })
        {
            using var response = await TaskTestClient.ChangeStatusAsync(
                workspace.Client, workspace.Slug, task.Id, status, Cancellation);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var current = await TaskTestClient.ReadDetailAsync(response, Cancellation);
            Assert.Equal(status, current.Status);
        }
    }

    /// <summary>
    /// Completion is recorded when a task is done and cleared when it is not.
    /// </summary>
    [Fact]
    public async Task Completion_is_recorded_and_cleared_with_the_status()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation);

        using var done = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, task.Id, TaskItemStatus.Done, Cancellation);
        var completed = await TaskTestClient.ReadDetailAsync(done, Cancellation);

        Assert.NotNull(completed.CompletedAt);

        using var reopened = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, task.Id, TaskItemStatus.Todo, Cancellation);
        var current = await TaskTestClient.ReadDetailAsync(reopened, Cancellation);

        Assert.Null(current.CompletedAt);
    }

    /// <summary>
    /// A finished task is never overdue, however late it was done.
    /// </summary>
    [Fact]
    public async Task Overdue_reflects_the_due_date_and_the_status()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var late = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Geciken görev",
            dueAt: DateTimeOffset.UtcNow.AddDays(-2));

        var upcoming = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Yaklaşan görev",
            dueAt: DateTimeOffset.UtcNow.AddDays(2));

        Assert.True(late.IsOverdue);
        Assert.False(upcoming.IsOverdue);

        using var done = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, late.Id, TaskItemStatus.Done, Cancellation);
        var finished = await TaskTestClient.ReadDetailAsync(done, Cancellation);

        Assert.False(finished.IsOverdue);
    }

    /// <summary>
    /// The update replaces every editable field, so a null clears rather than
    /// leaves alone.
    /// </summary>
    [Fact]
    public async Task Updating_with_nulls_clears_the_optional_fields()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation,
            customerId: workspace.CustomerId,
            assignedUserId: agent.Id,
            dueAt: DateTimeOffset.UtcNow.AddDays(3));

        Assert.NotNull(task.CustomerId);
        Assert.NotNull(task.AssignedUserId);
        Assert.NotNull(task.DueAt);

        using var response = await TaskTestClient.UpdateAsync(
            workspace.Client, workspace.Slug, task.Id, Cancellation, "Sadeleştirilmiş görev");
        var cleared = await TaskTestClient.ReadDetailAsync(response, Cancellation);

        Assert.Equal("Sadeleştirilmiş görev", cleared.Title);
        Assert.Null(cleared.CustomerId);
        Assert.Null(cleared.AssignedUserId);
        Assert.Null(cleared.DueAt);
        Assert.Null(cleared.Description);
    }

    [Fact]
    public async Task An_empty_title_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var response = await TaskTestClient.CreateAsync(
            workspace.Client, workspace.Slug, Cancellation, title: "   ");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_task_removes_it()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation);

        using var deleted = await TaskTestClient.DeleteAsync(
            workspace.Client, workspace.Slug, task.Id, Cancellation);
        using var reread = await TaskTestClient.GetAsync(
            workspace.Client, workspace.Slug, task.Id, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reread.StatusCode);
    }

    /// <summary>
    /// Archiving a customer leaves the work that was scheduled for them
    /// standing, with the customer still named.
    /// </summary>
    [Fact]
    public async Task A_task_survives_its_customer_being_archived()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var task = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, customerId: workspace.CustomerId);

        using var archived = await CustomerTestClient.ArchiveAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        using var reread = await TaskTestClient.GetAsync(
            workspace.Client, workspace.Slug, task.Id, Cancellation);
        var stillThere = await TaskTestClient.ReadDetailAsync(reread, Cancellation);

        Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
        Assert.Equal("Acme Teknoloji", stillThere.CustomerName);
    }
}
