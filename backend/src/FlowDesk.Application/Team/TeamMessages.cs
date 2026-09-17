using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Team;

/// <summary>
/// Someone was invited to a workspace.
/// </summary>
/// <remarks>
/// Carries the invitation token, which is the one place in the system where a
/// raw token travels outside the response that created it. It has to: the
/// e-mail is what the recipient clicks, and only a hash is stored
/// (docs/SECURITY.md).
///
/// <para>
/// That makes this message more sensitive than the others. It sits in the
/// outbox table and in the broker as readable text for as long as it takes to
/// deliver. The exposure is bounded the way the invitation itself is: the token
/// is single-use, expires in a week, and grants nothing beyond joining one
/// workspace at one role. It is not a credential for an account.
/// </para>
/// </remarks>
public sealed record MemberInvited(
    Guid MessageId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid InvitationId,
    string Email,
    MembershipRole Role,
    string Token,
    DateTimeOffset ExpiresAt,
    Guid InvitedByUserId,
    string WorkspaceSlug) : IntegrationMessage(MessageId, TenantId, OccurredAt)
{
    public const string Key = "team.member_invited";

    public override string RoutingKey => Key;
}
