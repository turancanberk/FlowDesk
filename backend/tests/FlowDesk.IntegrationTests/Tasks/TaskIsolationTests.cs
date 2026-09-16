using System.Net;
using FlowDesk.Domain.Tasks;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tasks;

/// <summary>
/// Tenant isolation for tasks.
/// </summary>
/// <remarks>
/// A security boundary, not a convenience check (docs/SECURITY.md). These tests
/// must never be deleted, skipped or weakened: a failure means one organisation
/// can read or change another's internal work.
///
/// A task points at a customer and at a person, and both are optional — which
/// makes the write-side ownership check easier to forget than on a ticket,
/// where the customer is required. That is what most of these cover.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TaskIsolationTests
{
    private readonly PostgresContainerFixture _postgres;

    public TaskIsolationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Listing_returns_only_the_current_workspaces_tasks()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var first = await TestWorkspace.CreateAsync(factory, Cancellation);
        await TaskTestClient.CreateAndReadAsync(
            first.Client, first.Slug, Cancellation, "Birinci alanın görevi");

        using var second = await TestWorkspace.CreateAsync(factory, Cancellation);
        await TaskTestClient.CreateAndReadAsync(
            second.Client, second.Slug, Cancellation, "İkinci alanın görevi");

        using var response = await TaskTestClient.ListAsync(
            second.Client, second.Slug, Cancellation);

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(page.Items);
        Assert.Equal("İkinci alanın görevi", page.Items[0].Title);
    }

    [Fact]
    public async Task A_stranger_cannot_read_or_change_another_workspaces_task()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await TestWorkspace.CreateAsync(factory, Cancellation);
        var task = await TaskTestClient.CreateAndReadAsync(owner.Client, owner.Slug, Cancellation);

        using var stranger = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var throughOwnWorkspace = await TaskTestClient.GetAsync(
            stranger.Client, stranger.Slug, task.Id, Cancellation);
        using var throughForeignWorkspace = await TaskTestClient.GetAsync(
            stranger.Client, owner.Slug, task.Id, Cancellation);
        using var moved = await TaskTestClient.ChangeStatusAsync(
            stranger.Client, stranger.Slug, task.Id, TaskItemStatus.Done, Cancellation);
        using var deleted = await TaskTestClient.DeleteAsync(
            stranger.Client, stranger.Slug, task.Id, Cancellation);

        // 404 in every case, never 403: a 403 would confirm the task exists
        // (ADR-0007).
        Assert.Equal(HttpStatusCode.NotFound, throughOwnWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, throughForeignWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, moved.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);

        using var reread = await TaskTestClient.GetAsync(
            owner.Client, owner.Slug, task.Id, Cancellation);
        var unchanged = await TaskTestClient.ReadDetailAsync(reread, Cancellation);

        Assert.Equal(TaskItemStatus.Todo, unchanged.Status);
    }

    /// <summary>
    /// A task cannot be tied to a customer from another workspace.
    /// </summary>
    /// <remarks>
    /// The query filter scopes what a request can read; it does not stop a
    /// foreign id arriving in a request body. This is the write-side ownership
    /// check.
    /// </remarks>
    [Fact]
    public async Task A_task_cannot_be_linked_to_a_foreign_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var foreign = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var mine = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var onCreate = await TaskTestClient.CreateAsync(
            mine.Client, mine.Slug, Cancellation, customerId: foreign.CustomerId);

        var task = await TaskTestClient.CreateAndReadAsync(mine.Client, mine.Slug, Cancellation);

        using var onUpdate = await TaskTestClient.UpdateAsync(
            mine.Client, mine.Slug, task.Id, Cancellation, customerId: foreign.CustomerId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, onCreate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, onUpdate.StatusCode);
        Assert.Equal(
            "task.customer_not_found",
            await AuthTestClient.ReadProblemCodeAsync(onUpdate, Cancellation));
    }

    [Fact]
    public async Task A_task_cannot_be_assigned_to_a_non_member()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var mine = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var outsider = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var onCreate = await TaskTestClient.CreateAsync(
            mine.Client, mine.Slug, Cancellation, assignedUserId: outsider.Id);

        var task = await TaskTestClient.CreateAndReadAsync(mine.Client, mine.Slug, Cancellation);

        using var onUpdate = await TaskTestClient.UpdateAsync(
            mine.Client, mine.Slug, task.Id, Cancellation, assignedUserId: outsider.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, onCreate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, onUpdate.StatusCode);
        Assert.Equal(
            "task.assignee_not_a_member",
            await AuthTestClient.ReadProblemCodeAsync(onUpdate, Cancellation));
    }
}
