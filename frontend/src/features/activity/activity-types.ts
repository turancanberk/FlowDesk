/** Matches FlowDesk.Domain ActivityType. */
export const ACTIVITY_TYPES = [
  "CustomerCreated",
  "CustomerArchived",
  "CustomerRestored",
  "TicketCreated",
  "TicketStatusChanged",
  "TicketAssigned",
  "TicketUnassigned",
  "TicketCommented",
  "TicketDeleted",
  "TaskCreated",
  "TaskCompleted",
  "TaskDeleted",
  "MemberInvited",
  "MemberJoined",
  "MemberRoleChanged",
  "MemberRemoved",
  "AttachmentUploaded",
  "AttachmentDeleted",
] as const;
export type ActivityKind = (typeof ACTIVITY_TYPES)[number];

/** Matches FlowDesk.Domain ActivitySubject. */
export const ACTIVITY_SUBJECTS = [
  "Customer",
  "Ticket",
  "TaskItem",
  "Member",
  "Attachment",
] as const;
export type ActivitySubject = (typeof ACTIVITY_SUBJECTS)[number];

export type ActivityItem = {
  id: string;
  actorUserId: string | null;
  /** Null when the system did it, or when the account is gone. */
  actorDisplayName: string | null;
  type: ActivityKind;
  subjectType: ActivitySubject;
  subjectId: string;
  /** Narrowed by `type` at the point of rendering; the server does not interpret it. */
  payload: unknown;
  occurredAt: string;
};

export type ActivityFilters = {
  type: ActivityKind | null;
  subjectType: ActivitySubject | null;
  page: number;
};

export const INITIAL_ACTIVITY_FILTERS: ActivityFilters = {
  type: null,
  subjectType: null,
  page: 1,
};
