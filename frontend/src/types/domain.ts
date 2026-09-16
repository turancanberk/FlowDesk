/*
  Domain kodları.

  Bu değerler API sözleşmesiyle birebir aynıdır ve İngilizce kalır. Kullanıcıya
  gösterilen Türkçe karşılıkları `src/lib/domain-labels.ts` içindedir; sunum
  dili domain koduna karışmaz (CLAUDE.md, "Domain Translation Boundary").
*/

export const TICKET_STATUSES = ["Open", "InProgress", "Waiting", "Resolved", "Closed"] as const;
export type TicketStatus = (typeof TICKET_STATUSES)[number];

export const TICKET_PRIORITIES = ["Low", "Medium", "High", "Urgent"] as const;
export type TicketPriority = (typeof TICKET_PRIORITIES)[number];

export const TASK_STATUSES = ["Todo", "InProgress", "Done"] as const;
export type TaskStatus = (typeof TASK_STATUSES)[number];

export const CUSTOMER_STATUSES = ["Active", "Inactive"] as const;
export type CustomerStatus = (typeof CUSTOMER_STATUSES)[number];

export const MEMBERSHIP_ROLES = ["Owner", "Admin", "Agent", "Viewer"] as const;
export type MembershipRole = (typeof MEMBERSHIP_ROLES)[number];
