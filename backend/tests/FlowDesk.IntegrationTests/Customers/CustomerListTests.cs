using System.Net;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Customers;

/// <summary>
/// Search, filtering, sorting, paging and the permissions around them.
/// </summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class CustomerListTests
{
    private readonly PostgresContainerFixture _postgres;

    public CustomerListTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Search must fold Turkish letters the way a Turkish user expects:
    /// "yazilim" has to find "Kuzey Yazılım".
    /// </summary>
    [Theory]
    [InlineData("kuzey", "Kuzey Yazılım")]
    [InlineData("KUZEY", "Kuzey Yazılım")]
    [InlineData("Yazılım", "Kuzey Yazılım")]
    [InlineData("YAZILIM", "Kuzey Yazılım")]
    [InlineData("acme", "Acme Teknoloji")]
    [InlineData("TEKNOLOJİ", "Acme Teknoloji")]
    public async Task Search_is_case_insensitive(string term, string expectedName)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Kuzey Yazılım", Cancellation);
        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);
        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Nova Lojistik", Cancellation);

        using var response = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, $"search={Uri.EscapeDataString(term)}");

        var page = await CustomerTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal(expectedName, page.Items[0].Name);
    }

    [Fact]
    public async Task Search_also_matches_company_and_email()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Ahmet Yılmaz", Cancellation,
            email: "ahmet@nova.test", company: "Nova Lojistik");
        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Selin Arslan", Cancellation);

        using var byCompany = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, "search=nova");
        using var byEmail = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, "search=ahmet%40nova");

        Assert.Single((await CustomerTestClient.ReadPageAsync(byCompany, Cancellation)).Items);
        Assert.Single((await CustomerTestClient.ReadPageAsync(byEmail, Cancellation)).Items);
    }

    [Fact]
    public async Task Status_filters_the_list()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Aktif Müşteri", Cancellation,
            status: CustomerStatus.Active);
        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Pasif Müşteri", Cancellation,
            status: CustomerStatus.Inactive);

        using var response = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, "status=Inactive");

        var page = await CustomerTestClient.ReadPageAsync(response, Cancellation);

        Assert.Single(page.Items);
        Assert.Equal("Pasif Müşteri", page.Items[0].Name);
    }

    [Fact]
    public async Task Sorting_by_name_orders_the_list()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        foreach (var name in new[] { "Cem", "Ali", "Berk" })
        {
            await CustomerTestClient.CreateAndReadAsync(
                owner.Client, workspace.Slug, name, Cancellation);
        }

        using var ascending = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, "sort=NameAscending");
        using var descending = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, "sort=NameDescending");

        var ascendingNames = (await CustomerTestClient.ReadPageAsync(ascending, Cancellation))
            .Items.Select(item => item.Name).ToArray();
        var descendingNames = (await CustomerTestClient.ReadPageAsync(descending, Cancellation))
            .Items.Select(item => item.Name).ToArray();

        Assert.Equal(["Ali", "Berk", "Cem"], ascendingNames);
        Assert.Equal(["Cem", "Berk", "Ali"], descendingNames);
    }

    /// <summary>
    /// Paging must be stable: every record appears exactly once across pages.
    /// </summary>
    /// <remarks>
    /// All eight customers share a creation moment and none of the sort columns
    /// is unique, which is precisely the case where a missing tiebreaker shows
    /// up as a duplicated or skipped row (docs/API_CONVENTIONS.md).
    /// </remarks>
    [Fact]
    public async Task Paging_covers_every_record_exactly_once()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        const int total = 8;

        for (var index = 0; index < total; index++)
        {
            await CustomerTestClient.CreateAndReadAsync(
                owner.Client, workspace.Slug, "Aynı Ad", Cancellation);
        }

        var seen = new List<Guid>();

        for (var page = 1; page <= 4; page++)
        {
            using var response = await CustomerTestClient.ListAsync(
                owner.Client, workspace.Slug, Cancellation, $"page={page}&pageSize=2");

            var result = await CustomerTestClient.ReadPageAsync(response, Cancellation);

            Assert.Equal(total, result.TotalCount);
            Assert.Equal(4, result.TotalPages);

            seen.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(total, seen.Count);
        Assert.Equal(total, seen.Distinct().Count());
    }

    /// <summary>
    /// Out-of-range paging is clamped rather than rejected: a client mistake
    /// should not break the user's screen, but the server still has to be
    /// protected.
    /// </summary>
    [Theory]
    [InlineData("pageSize=99999", 100)]
    [InlineData("pageSize=0", 25)]
    [InlineData("pageSize=-5", 25)]
    public async Task Page_size_is_clamped(string query, int expectedPageSize)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, query);

        var page = await CustomerTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedPageSize, page.PageSize);
    }

    [Fact]
    public async Task An_empty_workspace_returns_an_empty_page()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation);

        var page = await CustomerTestClient.ReadPageAsync(response, Cancellation);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    // ---- Permissions -------------------------------------------------------

    [Theory]
    [InlineData(MembershipRole.Viewer, false)]
    [InlineData(MembershipRole.Agent, true)]
    [InlineData(MembershipRole.Admin, true)]
    public async Task Only_agents_and_above_may_create_customers(
        MembershipRole role,
        bool allowed)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory, owner.Client, workspace.Slug, role, Cancellation);

        using var response = await CustomerTestClient.CreateAsync(
            member.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        Assert.Equal(
            allowed ? HttpStatusCode.Created : HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    /// <summary>
    /// Agents do the day-to-day work but taking a record out of circulation is
    /// an administrative act.
    /// </summary>
    [Theory]
    [InlineData(MembershipRole.Viewer)]
    [InlineData(MembershipRole.Agent)]
    public async Task Archiving_requires_an_admin(MembershipRole role)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory, owner.Client, workspace.Slug, role, Cancellation);

        using var response = await CustomerTestClient.ArchiveAsync(
            member.Client, workspace.Slug, customer.Id, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(MembershipRole.Viewer)]
    [InlineData(MembershipRole.Agent)]
    [InlineData(MembershipRole.Admin)]
    public async Task Every_member_may_view_customers(MembershipRole role)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory, owner.Client, workspace.Slug, role, Cancellation);

        using var response = await CustomerTestClient.ListAsync(
            member.Client, workspace.Slug, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await CustomerTestClient.ReadPageAsync(response, Cancellation)).Items);
    }

    [Fact]
    public async Task A_viewer_cannot_edit_a_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory, owner.Client, workspace.Slug, MembershipRole.Viewer, Cancellation);

        using var response = await CustomerTestClient.UpdateAsync(
            viewer.Client, workspace.Slug, customer.Id, "Yeni Ad", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_customer_must_have_a_name(string name)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await CustomerTestClient.CreateAsync(
            owner.Client, workspace.Slug, name, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_email_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await CustomerTestClient.CreateAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation,
            email: "gecersiz-eposta");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
