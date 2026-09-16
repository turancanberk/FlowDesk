using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Team.InviteMember;

public sealed record InviteMemberCommand(string Email, MembershipRole Role);
