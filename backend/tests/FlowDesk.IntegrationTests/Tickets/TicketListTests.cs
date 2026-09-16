using System.Net;
using FlowDesk.Application.Tickets;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tickets;

/// <summary>Filtering, searching, sorting and paging the ticket list.</summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class TicketListTests
{
    private readonly PostgresContainerFixture _postgres;

    public TicketListTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Status_and_priority_narrow_the_list()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var urgent = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Sunucu yanıt vermiyor", TicketPriority.Urgent);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Rapor başlığı yanlış", TicketPriority.Low);

        using var moved = await TicketTestClient.ChangeStatusAsync(
            workspace.Client, workspace.Slug, urgent.Id, TicketStatus.InProgress, Cancellation);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        using var byStatus = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "status=InProgress");
        using var byPriority = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "priority=Low");

        var statusPage = await TicketTestClient.ReadPageAsync(byStatus, Cancellation);
        var priorityPage = await TicketTestClient.ReadPageAsync(byPriority, Cancellation);

        Assert.Single(statusPage.Items);
        Assert.Equal("Sunucu yanıt vermiyor", statusPage.Items[0].Subject);

        Assert.Single(priorityPage.Items);
        Assert.Equal("Rapor başlığı yanlış", priorityPage.Items[0].Subject);
    }

    /// <summary>
    /// "Unassigned" is its own question, not the absence of an assignee filter.
    /// </summary>
    [Fact]
    public async Task Assignment_filters_separate_mine_from_nobodys()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation,
            "Zeynep Arslan");

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Atanmış talep", assignedUserId: agent.Id);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Kimseye atanmamış talep");

        using var mine = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, $"assignedUserId={agent.Id}");
        using var nobodys = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "unassigned=true");

        var minePage = await TicketTestClient.ReadPageAsync(mine, Cancellation);
        var nobodysPage = await TicketTestClient.ReadPageAsync(nobodys, Cancellation);

        Assert.Single(minePage.Items);
        Assert.Equal("Atanmış talep", minePage.Items[0].Subject);
        Assert.Equal("Zeynep Arslan", minePage.Items[0].AssignedUserDisplayName);

        Assert.Single(nobodysPage.Items);
        Assert.Equal("Kimseye atanmamış talep", nobodysPage.Items[0].Subject);
    }

    [Fact]
    public async Task The_customer_filter_backs_the_customer_detail_page()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var otherCustomer = await CustomerTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, "Kuzey Yazılım", Cancellation);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "Acme talebi");
        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, otherCustomer.Id, Cancellation, "Kuzey talebi");

        using var response = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, $"customerId={otherCustomer.Id}");

        var page = await TicketTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Kuzey talebi", page.Items[0].Subject);
        Assert.Equal("Kuzey Yazılım", page.Items[0].CustomerName);
    }

    /// <summary>
    /// Teams quote ticket numbers far more often than subjects, so a search that
    /// looks like a number is treated as one.
    /// </summary>
    [Theory]
    [InlineData("1002")]
    [InlineData("TLP-1002")]
    [InlineData("tlp-1002")]
    public async Task Searching_by_ticket_number_finds_exactly_that_ticket(string search)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "Birinci talep");
        var second = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation, "İkinci talep");

        Assert.Equal(TicketNumber.FirstNumber + 1, second.Number);

        using var response = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, $"search={Uri.EscapeDataString(search)}");

        var page = await TicketTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("İkinci talep", page.Items[0].Subject);
    }

    [Fact]
    public async Task Searching_by_text_matches_part_of_the_subject()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Fatura PDF'i indirilemiyor");
        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Rapor başlığı yanlış");

        using var response = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "search=fatura");

        var page = await TicketTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Fatura PDF'i indirilemiyor", page.Items[0].Subject);
    }

    [Fact]
    public async Task Sorting_by_priority_puts_the_most_urgent_first()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Düşük öncelikli", TicketPriority.Low);
        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Acil", TicketPriority.Urgent);
        await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
            "Orta öncelikli", TicketPriority.Medium);

        using var response = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation,
            $"sort={nameof(TicketSort.PriorityDescending)}");

        var page = await TicketTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(
            ["Acil", "Orta öncelikli", "Düşük öncelikli"],
            page.Items.Select(item => item.Subject));
    }

    /// <summary>
    /// Paging stays stable: every row appears exactly once across the pages.
    /// </summary>
    /// <remarks>
    /// Tickets created in the same second share a timestamp, so without the
    /// ticket number as a tiebreaker the order between them would be undefined
    /// and a row could show up on both pages or on neither.
    /// </remarks>
    [Fact]
    public async Task Paging_does_not_repeat_or_drop_rows()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        const int total = 7;

        for (var index = 0; index < total; index++)
        {
            await TicketTestClient.CreateAndReadAsync(
                workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
                $"Talep {index}");
        }

        using var firstResponse = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "page=1&pageSize=3");
        using var secondResponse = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "page=2&pageSize=3");
        using var thirdResponse = await TicketTestClient.ListAsync(
            workspace.Client, workspace.Slug, Cancellation, "page=3&pageSize=3");

        var first = await TicketTestClient.ReadPageAsync(firstResponse, Cancellation);
        var second = await TicketTestClient.ReadPageAsync(secondResponse, Cancellation);
        var third = await TicketTestClient.ReadPageAsync(thirdResponse, Cancellation);

        var seen = first.Items.Concat(second.Items).Concat(third.Items)
            .Select(item => item.Id)
            .ToList();

        Assert.Equal(total, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(total, seen.Count);
        Assert.Equal(total, seen.Distinct().Count());
    }
}
