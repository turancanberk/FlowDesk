using System.Net;
using FlowDesk.Application.Tasks;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tasks;

/// <summary>Filtering, searching, sorting and paging the task list.</summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class TaskListTests
{
    private readonly PostgresContainerFixture _postgres;

    public TaskListTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_narrows_the_list()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var done = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Biten görev");
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Bekleyen görev");

        using var moved = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, done.Id, TaskItemStatus.Done, Cancellation);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        using var response = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "status=Todo");

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Bekleyen görev", page.Items[0].Title);
    }

    /// <summary>
    /// The overdue filter is the one a task list exists for. Work finished late
    /// must not appear in it.
    /// </summary>
    [Fact]
    public async Task The_overdue_filter_excludes_finished_work()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Geciken görev",
            dueAt: DateTimeOffset.UtcNow.AddDays(-2));

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Yaklaşan görev",
            dueAt: DateTimeOffset.UtcNow.AddDays(2));

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Tarihsiz görev");

        var finishedLate = await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Geç biten görev",
            dueAt: DateTimeOffset.UtcNow.AddDays(-5));

        using var completed = await TaskTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, finishedLate.Id, TaskItemStatus.Done, Cancellation);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        using var response = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "overdue=true");

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Geciken görev", page.Items[0].Title);
        Assert.True(page.Items[0].IsOverdue);
    }

    [Fact]
    public async Task Assignment_filters_separate_mine_from_nobodys()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation,
            "Zeynep Arslan");

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Atanmış görev",
            assignedUserId: agent.Id);
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Kimseye atanmamış görev");

        using var mine = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, $"assignedUserId={agent.Id}");
        using var nobodys = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "unassigned=true");

        var minePage = await TaskTestClient.ReadPageAsync(mine, Cancellation);
        var nobodysPage = await TaskTestClient.ReadPageAsync(nobodys, Cancellation);

        Assert.Single(minePage.Items);
        Assert.Equal("Zeynep Arslan", minePage.Items[0].AssignedUserDisplayName);
        Assert.Single(nobodysPage.Items);
        Assert.Equal("Kimseye atanmamış görev", nobodysPage.Items[0].Title);
    }

    [Fact]
    public async Task The_customer_filter_backs_the_customer_detail_page()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Acme görevi",
            customerId: workspace.CustomerId);
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Müşterisiz görev");

        using var response = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation,
            $"customerId={workspace.CustomerId}");

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Acme görevi", page.Items[0].Title);
        Assert.Equal("Acme Teknoloji", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Searching_matches_part_of_the_title()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Sözleşmeyi gözden geçir");
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Fatura gönder");

        using var response = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "search=fatura");

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Fatura gönder", page.Items[0].Title);
    }

    /// <summary>
    /// The default order answers "what is due next", and undated work sits
    /// below anything someone has committed to a date.
    /// </summary>
    [Fact]
    public async Task The_default_order_puts_the_soonest_deadline_first_and_undated_work_last()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Tarihsiz");
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Gelecek hafta",
            dueAt: DateTimeOffset.UtcNow.AddDays(7));
        await TaskTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, Cancellation, "Yarın",
            dueAt: DateTimeOffset.UtcNow.AddDays(1));

        using var response = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation);

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(
            ["Yarın", "Gelecek hafta", "Tarihsiz"],
            page.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task Sorting_by_title_uses_the_requested_order()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        foreach (var title in new[] { "Cuma raporu", "Ajanda hazırla", "Bütçe kontrolü" })
        {
            await TaskTestClient.CreateAndReadAsync(
                workspace.Client, workspace.Slug, Cancellation, title);
        }

        using var response = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation,
            $"sort={nameof(TaskSort.TitleAscending)}");

        var page = await TaskTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(
            ["Ajanda hazırla", "Bütçe kontrolü", "Cuma raporu"],
            page.Items.Select(item => item.Title));
    }

    /// <summary>
    /// Paging stays stable: every row appears exactly once across the pages.
    /// </summary>
    /// <remarks>
    /// Tasks created without a due date all sort equally on the primary key of
    /// the default order, so without the id as a tiebreaker a row could show up
    /// on two pages or on none.
    /// </remarks>
    [Fact]
    public async Task Paging_does_not_repeat_or_drop_rows()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        const int total = 7;

        for (var index = 0; index < total; index++)
        {
            await TaskTestClient.CreateAndReadAsync(
                workspace.Client, workspace.Slug, Cancellation, $"Görev {index}");
        }

        using var firstResponse = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "page=1&pageSize=3");
        using var secondResponse = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "page=2&pageSize=3");
        using var thirdResponse = await TaskTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "page=3&pageSize=3");

        var first = await TaskTestClient.ReadPageAsync(firstResponse, Cancellation);
        var second = await TaskTestClient.ReadPageAsync(secondResponse, Cancellation);
        var third = await TaskTestClient.ReadPageAsync(thirdResponse, Cancellation);

        var seen = first.Items.Concat(second.Items).Concat(third.Items)
            .Select(item => item.Id)
            .ToList();

        Assert.Equal(total, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(total, seen.Count);
        Assert.Equal(total, seen.Distinct().Count());
    }
}
