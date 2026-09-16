import { apiFetch } from "@/lib/api/http-client";
import type { MembershipRole } from "@/features/workspaces/workspace-types";
import type {
  AcceptedInvitation,
  CreatedInvitation,
  PendingInvitation,
  TeamMember,
} from "./team-types";

const workspacePath = (slug: string) => `/api/workspaces/${encodeURIComponent(slug)}`;

export function listMembers(slug: string, signal?: AbortSignal): Promise<TeamMember[]> {
  return apiFetch<TeamMember[]>(`${workspacePath(slug)}/members`, { signal });
}

export function changeMemberRole(
  slug: string,
  userId: string,
  role: MembershipRole,
): Promise<void> {
  return apiFetch<void>(`${workspacePath(slug)}/members/${userId}`, {
    method: "PATCH",
    body: { role },
  });
}

export function removeMember(slug: string, userId: string): Promise<void> {
  return apiFetch<void>(`${workspacePath(slug)}/members/${userId}`, { method: "DELETE" });
}

export function listInvitations(slug: string, signal?: AbortSignal): Promise<PendingInvitation[]> {
  return apiFetch<PendingInvitation[]>(`${workspacePath(slug)}/invitations`, { signal });
}

export function inviteMember(
  slug: string,
  input: { email: string; role: MembershipRole },
): Promise<CreatedInvitation> {
  return apiFetch<CreatedInvitation>(`${workspacePath(slug)}/invitations`, {
    method: "POST",
    body: input,
  });
}

export function revokeInvitation(slug: string, invitationId: string): Promise<void> {
  return apiFetch<void>(`${workspacePath(slug)}/invitations/${invitationId}`, {
    method: "DELETE",
  });
}

export function acceptInvitation(token: string): Promise<AcceptedInvitation> {
  return apiFetch<AcceptedInvitation>("/api/invitations/accept", {
    method: "POST",
    body: { token },
  });
}
