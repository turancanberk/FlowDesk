using System.Net;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Team;

/// <summary>
/// The permission matrix from docs/SECURITY.md, enforced across HTTP.
/// </summary>
/// <remarks>
/// The matrix has a unit test of its own; this asserts that the endpoints
/// actually consult it. A correct table that no endpoint reads would grant
/// everyone everything while looking perfectly healthy in the unit tests.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class RoleAuthorizationTests
{
    private readonly PostgresContainerFixture _postgres;

    public RoleAuthorizationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(MembershipRole.Viewer, false)]
    [InlineData(MembershipRole.Agent, false)]
    [InlineData(MembershipRole.Admin, true)]
    public async Task Only_admins_and_owners_may_invite(MembershipRole role, bool allowed)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            role,
            Cancellation);

        using var response = await TeamTestClient.InviteAsync(
            member.Client,
            workspace.Slug,
            AuthTestClient.UniqueEmail(),
            MembershipRole.Agent,
            Cancellation);

        Assert.Equal(
            allowed ? HttpStatusCode.Created : HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Theory]
    [InlineData(MembershipRole.Viewer, false)]
    [InlineData(MembershipRole.Agent, false)]
    [InlineData(MembershipRole.Admin, true)]
    public async Task Only_admins_and_owners_may_rename_the_workspace(
        MembershipRole role,
        bool allowed)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            role,
            Cancellation);

        using var response = await WorkspaceTestClient.UpdateAsync(
            member.Client,
            workspace.Slug,
            "Yeni Ad",
            Cancellation);

        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Deleting a workspace is owner-only. An Admin manages the team; ending
    /// the organisation is a different kind of decision.
    /// </summary>
    [Theory]
    [InlineData(MembershipRole.Viewer)]
    [InlineData(MembershipRole.Agent)]
    [InlineData(MembershipRole.Admin)]
    public async Task Only_an_owner_may_delete_the_workspace(MembershipRole role)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            role,
            Cancellation);

        using var response = await WorkspaceTestClient.DeleteAsync(
            member.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And the workspace is still there.
        using var stillThere = await WorkspaceTestClient.GetAsync(
            owner.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Theory]
    [InlineData(MembershipRole.Viewer)]
    [InlineData(MembershipRole.Agent)]
    public async Task A_member_without_management_rights_cannot_change_roles(MembershipRole role)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            role,
            Cancellation);

        using var response = await TeamTestClient.ChangeRoleAsync(
            member.Client,
            workspace.Slug,
            member.Id,
            MembershipRole.Owner,
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// An Admin may manage the team but not the people above them. Otherwise
    /// the Owner role would carry no real authority.
    /// </summary>
    [Fact]
    public async Task An_admin_cannot_demote_an_owner()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var admin = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Admin,
            Cancellation);

        using var response = await TeamTestClient.ChangeRoleAsync(
            admin.Client,
            workspace.Slug,
            owner.Id,
            MembershipRole.Viewer,
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// An Admin cannot hand out ownership either; doing so through an accomplice
    /// would be a route to escalating their own authority.
    /// </summary>
    [Fact]
    public async Task An_admin_cannot_promote_anyone_to_owner()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var admin = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Admin,
            Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Agent,
            Cancellation,
            displayName: "Selin Arslan");

        using var response = await TeamTestClient.ChangeRoleAsync(
            admin.Client,
            workspace.Slug,
            agent.Id,
            MembershipRole.Owner,
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_invite_someone_as_owner()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var admin = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Admin,
            Cancellation);

        using var response = await TeamTestClient.InviteAsync(
            admin.Client,
            workspace.Slug,
            AuthTestClient.UniqueEmail(),
            MembershipRole.Owner,
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// The workspace must always keep someone who can administer it.
    /// </summary>
    [Fact]
    public async Task The_last_owner_cannot_be_demoted()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await TeamTestClient.ChangeRoleAsync(
            owner.Client,
            workspace.Slug,
            owner.Id,
            MembershipRole.Admin,
            Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "member.last_owner",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task The_last_owner_cannot_leave()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var response = await TeamTestClient.RemoveMemberAsync(
            owner.Client,
            workspace.Slug,
            owner.Id,
            Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_owner_may_step_down_once_another_owner_exists()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var second = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Owner,
            Cancellation);

        using var response = await TeamTestClient.ChangeRoleAsync(
            owner.Client,
            workspace.Slug,
            owner.Id,
            MembershipRole.Admin,
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Leaving is not the same act as removing someone else: every member may
    /// leave a workspace they joined.
    /// </summary>
    [Fact]
    public async Task Any_member_may_leave_on_their_own()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Viewer,
            Cancellation);

        using var response = await TeamTestClient.RemoveMemberAsync(
            viewer.Client,
            workspace.Slug,
            viewer.Id,
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Access ends immediately, with the same unexpired token.
        using var afterLeaving = await WorkspaceTestClient.GetAsync(
            viewer.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, afterLeaving.StatusCode);
    }

    [Fact]
    public async Task A_viewer_cannot_remove_another_member()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Viewer,
            Cancellation);

        using var agent = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Agent,
            Cancellation,
            displayName: "Selin Arslan");

        using var response = await TeamTestClient.RemoveMemberAsync(
            viewer.Client,
            workspace.Slug,
            agent.Id,
            Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Every member can see who else is in the workspace.</summary>
    [Theory]
    [InlineData(MembershipRole.Viewer)]
    [InlineData(MembershipRole.Agent)]
    [InlineData(MembershipRole.Admin)]
    public async Task Every_member_may_view_the_team(MembershipRole role)
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            role,
            Cancellation);

        using var response = await TeamTestClient.ListMembersAsync(
            member.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
