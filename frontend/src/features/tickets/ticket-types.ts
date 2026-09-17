import type { TicketPriority, TicketStatus } from "@/types/domain";

/** Matches FlowDesk.Application TicketSort. */
export const TICKET_SORTS = [
  "RecentlyUpdated",
  "RecentlyCreated",
  "PriorityDescending",
  "NumberDescending",
] as const;
export type TicketSort = (typeof TICKET_SORTS)[number];

export type TicketListItem = {
  id: string;
  /** Raw sequence number; rendered as TLP-1042 by {@link formatTicketNumber}. */
  number: number;
  subject: string;
  customerId: string;
  customerName: string;
  status: TicketStatus;
  priority: TicketPriority;
  assignedUserId: string | null;
  assignedUserDisplayName: string | null;
  createdAt: string;
  updatedAt: string;
};

export type TicketDetail = TicketListItem & {
  description: string;
  createdByUserId: string;
  createdByDisplayName: string | null;
  resolvedAt: string | null;
  /**
   * The moves the server would accept right now. The interface offers only
   * these, so it never shows a button the domain would refuse.
   */
  availableTransitions: TicketStatus[];
  /**
   * Row version, sent back on save. Lets the server report a colleague's
   * concurrent edit instead of silently overwriting it.
   */
  version: number;
};

export type TicketComment = {
  id: string;
  authorUserId: string;
  authorDisplayName: string;
  body: string;
  createdAt: string;
};

export type CreateTicketInput = {
  customerId: string;
  subject: string;
  description: string;
  priority: TicketPriority;
  assignedUserId: string | null;
};

export type UpdateTicketInput = {
  subject: string;
  description: string;
  priority: TicketPriority;
  customerId: string;
  version: number;
};

export type TicketFilters = {
  search: string;
  status: TicketStatus | null;
  priority: TicketPriority | null;
  customerId: string | null;
  assignedUserId: string | null;
  unassigned: boolean;
  sort: TicketSort;
  page: number;
};

export const INITIAL_TICKET_FILTERS: TicketFilters = {
  search: "",
  status: null,
  priority: null,
  customerId: null,
  assignedUserId: null,
  unassigned: false,
  sort: "RecentlyUpdated",
  page: 1,
};

/** TLP-1042 — the identifier a team and a customer quote to each other. */
export function formatTicketNumber(value: number): string {
  return `TLP-${value}`;
}

export type TicketAttachment = {
  id: string;
  fileName: string;
  contentType: string;
  /** Raw bytes; formatted for display at the presentation layer. */
  sizeInBytes: number;
  uploadedByUserId: string;
  createdAt: string;
};
