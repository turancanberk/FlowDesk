using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.Team;

/// <summary>
/// Invitation lifecycle and the rules that make an invitation link safe to
/// send (docs/SECURITY.md).
/// </summary>
[Collection(IntegrationTestSuite.Name)]
public sealed class InvitationTests
{
    private readonly PostgresContainerFixture _postgres;

    public InvitationTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invited_person_joins_with_the_role_they_were_offered()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var invited = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Agent,
            Cancellation);

        using var response = await TeamTestClient.ListMembersAsync(
            owner.Client,
            workspace.Slug,
            Cancellation);

        var members = await response.Content.ReadFromJsonAsync<List<TeamMemberResponse>>(
            FlowDeskJson.Options,
            Cancellation);

        Assert.NotNull(members);
        var member = Assert.Single(members, candidate => candidate.UserId == invited.Id);
        Assert.Equal(MembershipRole.Agent, member.Role);
    }

    /// <summary>
    /// An invitation is single use. Otherwise a forwarded link would keep
    /// granting access to whoever received it.
    /// </summary>
    [Fact]
    public async Task An_invitation_cannot_be_accepted_twice()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var invited = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            invited.Email,
            MembershipRole.Agent,
            Cancellation);

        using var first = await TeamTestClient.AcceptAsync(invited.Client, created.Token, Cancellation);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await TeamTestClient.AcceptAsync(invited.Client, created.Token, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        Assert.Equal(
            "invitation.not_usable",
            await AuthTestClient.ReadProblemCodeAsync(second, Cancellation));
    }

    /// <summary>
    /// The link is bound to the invited address, so forwarding it to someone
    /// else achieves nothing.
    /// </summary>
    [Fact]
    public async Task A_forwarded_invitation_cannot_be_redeemed_by_someone_else()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var intended = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            intended.Email,
            MembershipRole.Agent,
            Cancellation);

        using var response = await TeamTestClient.AcceptAsync(
            stranger.Client,
            created.Token,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The stranger did not become a member.
        using var strangerView = await WorkspaceTestClient.GetAsync(
            stranger.Client,
            workspace.Slug,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, strangerView.StatusCode);
    }

    [Fact]
    public async Task An_expired_invitation_cannot_be_accepted()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var invited = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            invited.Email,
            MembershipRole.Agent,
            Cancellation);

        // Aged in the database rather than waited out.
        await using (var dbContext = _postgres.CreateDbContext())
        {
            await dbContext.Invitations
                .IgnoreQueryFilters()
                .Where(invitation => invitation.Id == created.Invitation.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        invitation => invitation.ExpiresAt,
                        DateTimeOffset.UtcNow.AddDays(-1)),
                    Cancellation);
        }

        using var response = await TeamTestClient.AcceptAsync(
            invited.Client,
            created.Token,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_withdrawn_invitation_cannot_be_accepted()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var invited = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            invited.Email,
            MembershipRole.Agent,
            Cancellation);

        using var revoked = await TeamTestClient.RevokeInvitationAsync(
            owner.Client,
            workspace.Slug,
            created.Invitation.Id,
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using var response = await TeamTestClient.AcceptAsync(
            invited.Client,
            created.Token,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Only a hash is persisted, so a database leak yields no redeemable links.
    /// </summary>
    [Fact]
    public async Task The_raw_invitation_token_is_never_stored()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            AuthTestClient.UniqueEmail(),
            MembershipRole.Agent,
            Cancellation);

        await using var dbContext = _postgres.CreateDbContext();

        var storedHash = await dbContext.Invitations
            .IgnoreQueryFilters()
            .Where(invitation => invitation.Id == created.Invitation.Id)
            .Select(invitation => invitation.TokenHash)
            .SingleAsync(Cancellation);

        Assert.NotEqual(created.Token, storedHash);
        Assert.Equal(64, storedHash.Length);
    }

    /// <summary>
    /// Every failed acceptance answers the same way, so a caller cannot learn
    /// whether a token exists by comparing responses.
    /// </summary>
    [Fact]
    public async Task Every_failed_acceptance_answers_identically()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var intended = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            intended.Email,
            MembershipRole.Agent,
            Cancellation);

        using var wrongPerson = await TeamTestClient.AcceptAsync(
            stranger.Client,
            created.Token,
            Cancellation);

        using var inventedToken = await TeamTestClient.AcceptAsync(
            stranger.Client,
            "tamamen-uydurulmus-bir-davet-tokeni",
            Cancellation);

        Assert.Equal(wrongPerson.StatusCode, inventedToken.StatusCode);
        Assert.Equal(
            await AuthTestClient.ReadProblemCodeAsync(wrongPerson, Cancellation),
            await AuthTestClient.ReadProblemCodeAsync(inventedToken, Cancellation));
    }

    [Fact]
    public async Task Inviting_an_existing_member_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        using var member = await TeamTestClient.AddMemberAsync(
            factory,
            owner.Client,
            workspace.Slug,
            MembershipRole.Agent,
            Cancellation);

        using var response = await TeamTestClient.InviteAsync(
            owner.Client,
            workspace.Slug,
            member.Email,
            MembershipRole.Agent,
            Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "member.already_exists",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_second_pending_invitation_to_the_same_address_is_rejected()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        var email = AuthTestClient.UniqueEmail();

        await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            email,
            MembershipRole.Agent,
            Cancellation);

        using var second = await TeamTestClient.InviteAsync(
            owner.Client,
            workspace.Slug,
            email,
            MembershipRole.Agent,
            Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Invitations belong to one workspace. A member of another must not be
    /// able to see or withdraw them.
    /// </summary>
    [Fact]
    public async Task A_stranger_cannot_see_or_withdraw_another_workspaces_invitations()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var owner = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(owner.Client, Cancellation);

        var created = await TeamTestClient.InviteAndReadAsync(
            owner.Client,
            workspace.Slug,
            AuthTestClient.UniqueEmail(),
            MembershipRole.Agent,
            Cancellation);

        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var list = await TeamTestClient.ListInvitationsAsync(
            stranger.Client,
            workspace.Slug,
            Cancellation);

        using var revoke = await TeamTestClient.RevokeInvitationAsync(
            stranger.Client,
            workspace.Slug,
            created.Invitation.Id,
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }
}
