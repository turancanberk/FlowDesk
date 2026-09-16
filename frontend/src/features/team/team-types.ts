import type { MembershipRole } from "@/features/workspaces/workspace-types";

export type TeamMember = {
  userId: string;
  email: string;
  displayName: string;
  role: MembershipRole;
  joinedAt: string;
  /**
   * Lets the interface disable the controls that would break the last-owner
   * rule instead of offering them and then showing a rejection. The backend
   * enforces the rule regardless.
   */
  isLastOwner: boolean;
};

export type PendingInvitation = {
  id: string;
  email: string;
  role: MembershipRole;
  createdAt: string;
  expiresAt: string;
};

export type CreatedInvitation = {
  invitation: PendingInvitation;
  /** Returned exactly once; only a hash is stored server-side. */
  token: string;
};

export type AcceptedInvitation = {
  tenantId: string;
  workspaceSlug: string;
  workspaceName: string;
};
