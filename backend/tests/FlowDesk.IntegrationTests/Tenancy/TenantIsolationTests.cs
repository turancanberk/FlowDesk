using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Tenancy;

/// <summary>
/// Tenant isolation, exercised across the HTTP boundary.
/// </summary>
/// <remarks>
/// These tests are a security boundary, not a convenience check
/// (docs/SECURITY.md). They must never be deleted, skipped or have their
/// assertions loosened to make a change pass. A failure here means one
/// organisation can reach another's data.
///
/// Every case asserts <c>404</c> rather than <c>403</c>. Returning "forbidden"
/// would confirm that the workspace exists, letting anyone enumerate
/// organisations by guessing slugs (ADR-0007).
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TenantIsolationTests
{
    private readonly PostgresContainerFixture _postgres;

    public TenantIsolationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_stranger_cannot_read_another_workspace()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation, "Ayşe Demir");

        using var response = await WorkspaceTestClient.GetAsync(
            stranger.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_stranger_cannot_update_another_workspace()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation, "Mehmet Kaya");

        using var response = await WorkspaceTestClient.UpdateAsync(
            stranger.Client,
            workspace.Slug,
            "Ele Geçirilen Alan",
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And the name really did not change.
        using var reread = await WorkspaceTestClient.GetAsync(owner.Client, workspace.Slug, Cancellation);
        var unchanged = await reread.Content.ReadFromJsonAsync<WorkspaceResponse>(FlowDeskJson.Options, Cancellation);

        Assert.NotNull(unchanged);
        Assert.Equal(workspace.Name, unchanged.Name);
    }

    [Fact]
    public async Task A_stranger_cannot_delete_another_workspace()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation, "Selin Arslan");

        using var response = await WorkspaceTestClient.DeleteAsync(
            stranger.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The workspace survived.
        using var reread = await WorkspaceTestClient.GetAsync(owner.Client, workspace.Slug, Cancellation);
        Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
    }

    /// <summary>
    /// Listing must answer "the workspaces I belong to", never "the workspaces
    /// that exist".
    /// </summary>
    [Fact]
    public async Task Listing_returns_only_the_callers_own_workspaces()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var first = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var firstWorkspace = await WorkspaceTestClient.CreateOwnedAsync(
            first.Client,
            Cancellation,
            "Acme Teknoloji");

        using var second = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var secondWorkspace = await WorkspaceTestClient.CreateOwnedAsync(
            second.Client,
            Cancellation,
            "Kuzey Yazılım");

        using var response = await WorkspaceTestClient.ListAsync(second.Client, Cancellation);
        var workspaces = await response.Content.ReadFromJsonAsync<List<WorkspaceResponse>>(FlowDeskJson.Options, Cancellation);

        Assert.NotNull(workspaces);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(workspaces, workspace => workspace.Slug == secondWorkspace.Slug);
        Assert.DoesNotContain(workspaces, workspace => workspace.Slug == firstWorkspace.Slug);
    }

    /// <summary>
    /// The slug comes from the URL, which the caller controls. Guessing a real
    /// one must not be enough.
    /// </summary>
    [Fact]
    public async Task Knowing_the_slug_is_not_enough_to_reach_a_workspace()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var existing = await WorkspaceTestClient.GetAsync(
            stranger.Client,
            workspace.Slug,
            Cancellation);

        using var invented = await WorkspaceTestClient.GetAsync(
            stranger.Client,
            "hic-var-olmayan-calisma-alani",
            Cancellation);

        // Identical answers: a caller cannot tell an existing workspace from
        // one that was never created.
        Assert.Equal(HttpStatusCode.NotFound, existing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);
        Assert.Equal(
            await AuthTestClient.ReadProblemCodeAsync(existing, Cancellation),
            await AuthTestClient.ReadProblemCodeAsync(invented, Cancellation));
    }

    [Fact]
    public async Task Workspace_routes_reject_anonymous_callers()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var anonymous = factory.CreateApiClient();

        using var read = await WorkspaceTestClient.GetAsync(anonymous, workspace.Slug, Cancellation);
        using var list = await WorkspaceTestClient.ListAsync(anonymous, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    /// <summary>
    /// Losing access must take effect immediately, not when the access token
    /// happens to expire.
    /// </summary>
    [Fact]
    public async Task Removing_a_membership_cuts_access_off_at_once()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var before = await WorkspaceTestClient.GetAsync(owner.Client, workspace.Slug, Cancellation);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Membership management arrives in Faz 05; the row is removed directly
        // so the resolution path can be tested now.
        await using (var dbContext = _postgres.CreateDbContext())
        {
            var membership = dbContext.Memberships.Single(candidate =>
                candidate.TenantId == workspace.Id && candidate.UserId == owner.Id);

            dbContext.Memberships.Remove(membership);
            await dbContext.SaveChangesAsync(Cancellation);
        }

        // Same bearer token, still perfectly valid and unexpired.
        using var after = await WorkspaceTestClient.GetAsync(owner.Client, workspace.Slug, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }
}
