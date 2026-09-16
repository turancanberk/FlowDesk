using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Team.AcceptInvitation;
using FlowDesk.Application.Team.InviteMember;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.IntegrationTests.Support;

internal static class TeamTestClient
{
    public static Task<HttpResponseMessage> ListMembersAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken) =>
        client.GetAsync(new Uri($"/api/workspaces/{slug}/members", UriKind.Relative), cancellationToken);

    public static Task<HttpResponseMessage> ChangeRoleAsync(
        HttpClient client,
        string slug,
        Guid userId,
        MembershipRole role,
        CancellationToken cancellationToken) =>
        client.PatchAsJsonAsync(
            new Uri($"/api/workspaces/{slug}/members/{userId}", UriKind.Relative),
            new ChangeMemberRoleRequest(role),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> RemoveMemberAsync(
        HttpClient client,
        string slug,
        Guid userId,
        CancellationToken cancellationToken) =>
        client.DeleteAsync(
            new Uri($"/api/workspaces/{slug}/members/{userId}", UriKind.Relative),
            cancellationToken);

    public static Task<HttpResponseMessage> InviteAsync(
        HttpClient client,
        string slug,
        string email,
        MembershipRole role,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri($"/api/workspaces/{slug}/invitations", UriKind.Relative),
            new InviteMemberCommand(email, role),
            FlowDeskJson.Options,
            cancellationToken);

    public static Task<HttpResponseMessage> ListInvitationsAsync(
        HttpClient client,
        string slug,
        CancellationToken cancellationToken) =>
        client.GetAsync(
            new Uri($"/api/workspaces/{slug}/invitations", UriKind.Relative),
            cancellationToken);

    public static Task<HttpResponseMessage> RevokeInvitationAsync(
        HttpClient client,
        string slug,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        client.DeleteAsync(
            new Uri($"/api/workspaces/{slug}/invitations/{invitationId}", UriKind.Relative),
            cancellationToken);

    public static Task<HttpResponseMessage> AcceptAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(
            new Uri("/api/invitations/accept", UriKind.Relative),
            new AcceptInvitationCommand(token),
            FlowDeskJson.Options,
            cancellationToken);

    /// <summary>Invites an address and returns the one-time token.</summary>
    public static async Task<CreatedInvitationResponse> InviteAndReadAsync(
        HttpClient client,
        string slug,
        string email,
        MembershipRole role,
        CancellationToken cancellationToken)
    {
        using var response = await InviteAsync(client, slug, email, role, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreatedInvitationResponse>(
            FlowDeskJson.Options,
            cancellationToken);

        Assert.NotNull(created);

        return created;
    }

    /// <summary>
    /// Registers a user, invites them at the given role and accepts on their
    /// behalf, so a test can start from "a workspace with an Agent in it".
    /// </summary>
    public static async Task<SignedInUser> AddMemberAsync(
        FlowDeskApiFactory factory,
        HttpClient ownerClient,
        string slug,
        MembershipRole role,
        CancellationToken cancellationToken,
        string displayName = "Mehmet Kaya")
    {
        var member = await AuthTestClient.SignInNewUserAsync(factory, cancellationToken, displayName);

        var created = await InviteAndReadAsync(ownerClient, slug, member.Email, role, cancellationToken);

        using var accepted = await AcceptAsync(member.Client, created.Token, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        return member;
    }
}
