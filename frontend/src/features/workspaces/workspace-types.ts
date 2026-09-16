/** Matches FlowDesk.Domain MembershipRole. Values stay English; labels are mapped for display. */
export const MEMBERSHIP_ROLES = ["Viewer", "Agent", "Admin", "Owner"] as const;
export type MembershipRole = (typeof MEMBERSHIP_ROLES)[number];

export type Workspace = {
  id: string;
  name: string;
  slug: string;
  /** The signed-in user's role in this workspace. */
  role: MembershipRole;
  createdAt: string;
};
