using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Team;

/// <param name="IsLastOwner">
/// Lets the interface disable the controls that would break the
/// last-owner rule, instead of offering them and then rejecting the attempt.
/// The backend enforces the rule regardless.
/// </param>
public sealed record TeamMember(
    Guid UserId,
    string Email,
    string DisplayName,
    MembershipRole Role,
    DateTimeOffset JoinedAt,
    bool IsLastOwner);

public sealed record PendingInvitation(
    Guid Id,
    string Email,
    MembershipRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

/// <param name="Token">
/// The raw invitation token, returned exactly once at creation.
/// </param>
/// <remarks>
/// Only a hash is stored, so this value cannot be recovered afterwards. Until
/// e-mail delivery exists (Faz 12) the interface shows the link so an admin can
/// pass it on; once mail is wired up the token goes to the queue instead and
/// stops being part of this response.
/// </remarks>
public sealed record CreatedInvitation(PendingInvitation Invitation, string Token);

/// <summary>Which workspace an accepted invitation led into.</summary>
public sealed record AcceptedInvitation(Guid TenantId, string WorkspaceSlug, string WorkspaceName);
