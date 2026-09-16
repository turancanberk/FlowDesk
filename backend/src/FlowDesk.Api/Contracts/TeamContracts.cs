using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Api.Contracts;

/// <summary>
/// The member id comes from the route, so the body carries only the new role.
/// </summary>
public sealed record ChangeMemberRoleRequest(MembershipRole Role);

public sealed record TeamMemberResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    MembershipRole Role,
    DateTimeOffset JoinedAt,
    bool IsLastOwner);

public sealed record PendingInvitationResponse(
    Guid Id,
    string Email,
    MembershipRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// The created invitation together with its token, returned exactly once.
/// </summary>
/// <remarks>
/// Only a hash is stored, so the token cannot be recovered later. Until e-mail
/// delivery exists (Faz 12), the interface shows the link so an admin can pass
/// it on; once mail is wired up the token goes to the queue instead and leaves
/// this response.
/// </remarks>
public sealed record CreatedInvitationResponse(PendingInvitationResponse Invitation, string Token);

public sealed record AcceptedInvitationResponse(
    Guid TenantId,
    string WorkspaceSlug,
    string WorkspaceName);
