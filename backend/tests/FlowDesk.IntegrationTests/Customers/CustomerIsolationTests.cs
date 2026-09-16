using System.Net;
using FlowDesk.Domain.Customers;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.Customers;

/// <summary>
/// Tenant isolation for customers — the first business entity to rely on the
/// global query filter (ADR-0024).
/// </summary>
/// <remarks>
/// A security boundary, not a convenience check (docs/SECURITY.md). These tests
/// must never be deleted, skipped or weakened: a failure means one organisation
/// can read or change another's customer records.
///
/// Where Faz 04 verified the filter's presence on the model, these verify its
/// behaviour against real rows sharing one table.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class CustomerIsolationTests
{
    private readonly PostgresContainerFixture _postgres;

    public CustomerIsolationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Two workspaces, two customers, one table. Each list must contain only
    /// its own.
    /// </summary>
    [Fact]
    public async Task Listing_returns_only_the_current_workspaces_customers()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var first = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var firstWorkspace = await WorkspaceTestClient.CreateOwnedAsync(first.Client, Cancellation);
        await CustomerTestClient.CreateAndReadAsync(
            first.Client, firstWorkspace.Slug, "Acme Teknoloji", Cancellation);

        using var second = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var secondWorkspace = await WorkspaceTestClient.CreateOwnedAsync(second.Client, Cancellation);
        await CustomerTestClient.CreateAndReadAsync(
            second.Client, secondWorkspace.Slug, "Kuzey Yazılım", Cancellation);

        using var response = await CustomerTestClient.ListAsync(
            second.Client, secondWorkspace.Slug, Cancellation);

        var page = await CustomerTestClient.ReadPageAsync(response, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(page.Items);
        Assert.Equal("Kuzey Yazılım", page.Items[0].Name);
    }

    [Fact]
    public async Task A_stranger_cannot_read_another_workspaces_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var strangerWorkspace = await WorkspaceTestClient.CreateOwnedAsync(
            stranger.Client, Cancellation);

        // Through the stranger's own workspace, using the other customer's id.
        using var throughOwnWorkspace = await CustomerTestClient.GetAsync(
            stranger.Client, strangerWorkspace.Slug, customer.Id, Cancellation);

        // And directly against the workspace they do not belong to.
        using var throughForeignWorkspace = await CustomerTestClient.GetAsync(
            stranger.Client, workspace.Slug, customer.Id, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, throughOwnWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, throughForeignWorkspace.StatusCode);
    }

    [Fact]
    public async Task A_stranger_cannot_update_another_workspaces_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var strangerWorkspace = await WorkspaceTestClient.CreateOwnedAsync(
            stranger.Client, Cancellation);

        using var response = await CustomerTestClient.UpdateAsync(
            stranger.Client, strangerWorkspace.Slug, customer.Id, "Ele Geçirildi", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The record really did not change.
        using var reread = await CustomerTestClient.GetAsync(
            owner.Client, workspace.Slug, customer.Id, Cancellation);
        var body = await reread.Content.ReadAsStringAsync(Cancellation);

        Assert.Contains("Acme Teknoloji", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Ele Geçirildi", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stranger_cannot_archive_another_workspaces_customer()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var strangerWorkspace = await WorkspaceTestClient.CreateOwnedAsync(
            stranger.Client, Cancellation);

        using var response = await CustomerTestClient.ArchiveAsync(
            stranger.Client, strangerWorkspace.Slug, customer.Id, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var stillVisible = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation);
        var page = await CustomerTestClient.ReadPageAsync(stillVisible, Cancellation);

        Assert.Single(page.Items);
    }

    /// <summary>
    /// The workspace is taken from the resolved request context, never from the
    /// request body, so a client cannot plant a record in another organisation.
    /// </summary>
    [Fact]
    public async Task A_created_customer_belongs_to_the_workspace_in_the_route()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Nova Lojistik", Cancellation);

        await using var dbContext = _postgres.CreateDbContext();

        var storedTenantId = await dbContext.Customers
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Id == customer.Id)
            .Select(candidate => candidate.TenantId)
            .SingleAsync(Cancellation);

        Assert.Equal(workspace.Id, storedTenantId);
    }

    /// <summary>
    /// Archiving is not deletion: the row survives so that tickets and tasks
    /// referring to it keep their meaning (ADR-0012).
    /// </summary>
    [Fact]
    public async Task An_archived_customer_leaves_the_list_but_stays_in_the_database()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var archived = await CustomerTestClient.ArchiveAsync(
            owner.Client, workspace.Slug, customer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        using var defaultList = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation);
        var defaultPage = await CustomerTestClient.ReadPageAsync(defaultList, Cancellation);

        Assert.Empty(defaultPage.Items);

        // Still readable through its detail page, so old links keep working.
        using var detail = await CustomerTestClient.GetAsync(
            owner.Client, workspace.Slug, customer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        await using var dbContext = _postgres.CreateDbContext();
        var exists = await dbContext.Customers
            .IgnoreQueryFilters()
            .AnyAsync(candidate => candidate.Id == customer.Id, Cancellation);

        Assert.True(exists);
    }

    [Fact]
    public async Task Archived_customers_appear_only_when_asked_for()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        var kept = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Kalan Müşteri", Cancellation);
        var archivedCustomer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Arşivlenen Müşteri", Cancellation);

        using var archive = await CustomerTestClient.ArchiveAsync(
            owner.Client, workspace.Slug, archivedCustomer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        using var withArchived = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation, "includeArchived=true");
        var page = await CustomerTestClient.ReadPageAsync(withArchived, Cancellation);

        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, item => item.Id == kept.Id && !item.IsArchived);
        Assert.Contains(page.Items, item => item.Id == archivedCustomer.Id && item.IsArchived);
    }

    /// <summary>
    /// Including archived rows bypasses the global filter, which also carries
    /// the workspace scope. This asserts the scope was re-applied by hand.
    /// </summary>
    [Fact]
    public async Task Including_archived_rows_does_not_reach_across_workspaces()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var first = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var firstWorkspace = await WorkspaceTestClient.CreateOwnedAsync(first.Client, Cancellation);
        var foreignCustomer = await CustomerTestClient.CreateAndReadAsync(
            first.Client, firstWorkspace.Slug, "Yabancı Müşteri", Cancellation);

        using var archive = await CustomerTestClient.ArchiveAsync(
            first.Client, firstWorkspace.Slug, foreignCustomer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        using var second = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var secondWorkspace = await WorkspaceTestClient.CreateOwnedAsync(second.Client, Cancellation);

        using var response = await CustomerTestClient.ListAsync(
            second.Client, secondWorkspace.Slug, Cancellation, "includeArchived=true");

        var page = await CustomerTestClient.ReadPageAsync(response, Cancellation);

        Assert.Empty(page.Items);
        Assert.DoesNotContain(page.Items, item => item.Id == foreignCustomer.Id);
    }

    [Fact]
    public async Task A_restored_customer_returns_to_the_list()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var archive = await CustomerTestClient.ArchiveAsync(
            owner.Client, workspace.Slug, customer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        using var restore = await CustomerTestClient.RestoreAsync(
            owner.Client, workspace.Slug, customer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        using var list = await CustomerTestClient.ListAsync(
            owner.Client, workspace.Slug, Cancellation);
        var page = await CustomerTestClient.ReadPageAsync(list, Cancellation);

        Assert.Single(page.Items);
        Assert.False(page.Items[0].IsArchived);
    }

    [Fact]
    public async Task An_archived_customer_cannot_be_edited()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation);

        using var archive = await CustomerTestClient.ArchiveAsync(
            owner.Client, workspace.Slug, customer.Id, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        using var response = await CustomerTestClient.UpdateAsync(
            owner.Client, workspace.Slug, customer.Id, "Yeni Ad", Cancellation);

        // The filter hides archived rows from the update path, so it reads as
        // not found rather than as a conflict.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_routes_reject_anonymous_callers()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var anonymous = factory.CreateApiClient();

        using var list = await CustomerTestClient.ListAsync(anonymous, workspace.Slug, Cancellation);
        using var create = await CustomerTestClient.CreateAsync(
            anonymous, workspace.Slug, "Acme", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task Customer_status_is_serialised_by_name()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await CustomerTestClient.CreateAsync(
            owner.Client, workspace.Slug, "Acme Teknoloji", Cancellation,
            status: CustomerStatus.Inactive);

        var body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Contains("\"status\":\"Inactive\"", body, StringComparison.Ordinal);
    }
}
