import type { TaskStatus } from "@/types/domain";

/** Matches FlowDesk.Application TaskSort. */
export const TASK_SORTS = [
  "DueSoonest",
  "RecentlyCreated",
  "RecentlyUpdated",
  "TitleAscending",
] as const;
export type TaskSort = (typeof TASK_SORTS)[number];

export type TaskListItem = {
  id: string;
  title: string;
  status: TaskStatus;
  dueAt: string | null;
  /** Computed server-side, so every client agrees on what is late. */
  isOverdue: boolean;
  assignedUserId: string | null;
  assignedUserDisplayName: string | null;
  customerId: string | null;
  customerName: string | null;
  createdAt: string;
  updatedAt: string;
};

export type TaskDetail = TaskListItem & {
  description: string | null;
  createdByUserId: string;
  createdByDisplayName: string | null;
  completedAt: string | null;
};

/**
 * Every editable field, always sent.
 *
 * The update replaces rather than patches, so null means "none" instead of
 * "leave alone" — an ambiguity a partial body cannot avoid, because JSON
 * delivers an absent field and a null field the same way.
 */
export type TaskInput = {
  title: string;
  description: string | null;
  customerId: string | null;
  assignedUserId: string | null;
  dueAt: string | null;
};

export type TaskFilters = {
  search: string;
  status: TaskStatus | null;
  customerId: string | null;
  assignedUserId: string | null;
  unassigned: boolean;
  overdue: boolean;
  sort: TaskSort;
  page: number;
};

export const INITIAL_TASK_FILTERS: TaskFilters = {
  search: "",
  status: null,
  customerId: null,
  assignedUserId: null,
  unassigned: false,
  overdue: false,
  sort: "DueSoonest",
  page: 1,
};
