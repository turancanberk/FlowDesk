/** Matches FlowDesk.Domain NotificationType. */
export const NOTIFICATION_TYPES = ["TicketAssigned", "TicketCommented"] as const;
export type NotificationKind = (typeof NOTIFICATION_TYPES)[number];

export type TicketAssignedPayload = {
  ticketId: string;
  ticketNumber: number;
  subject: string;
  assignedByDisplayName: string;
  workspaceSlug: string;
};

export type TicketCommentedPayload = {
  ticketId: string;
  ticketNumber: number;
  subject: string;
  authorDisplayName: string;
  excerpt: string;
  workspaceSlug: string;
};

/**
 * One notice, with its payload still untyped.
 *
 * The server does not interpret the payload — what a notice looks like is a
 * presentation decision — so it arrives as an object and is narrowed by `type`
 * at the point of rendering.
 */
export type NotificationItem = {
  id: string;
  type: NotificationKind;
  payload: unknown;
  isRead: boolean;
  createdAt: string;
};

export type NotificationFeed = {
  items: NotificationItem[];
  /** Counted across everything, not just what came back. */
  unreadCount: number;
};
