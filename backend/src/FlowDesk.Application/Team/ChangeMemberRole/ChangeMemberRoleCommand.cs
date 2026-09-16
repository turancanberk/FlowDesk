using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Team.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(Guid UserId, MembershipRole Role);
