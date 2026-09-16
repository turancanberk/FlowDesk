import { apiFetch } from "@/lib/api/http-client";
import type { TaskStatus } from "@/types/domain";
import type { PagedResponse } from "@/features/customers/customer-types";
import type { TaskDetail, TaskFilters, TaskInput, TaskListItem } from "./task-types";

const basePath = (slug: string) => `/api/workspaces/${encodeURIComponent(slug)}/tasks`;

export function listTasks(
  slug: string,
  filters: TaskFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<TaskListItem>> {
  const query = new URLSearchParams();

  // Only non-default values are sent, which keeps the request URL — and the
  // query cache key derived from it — readable.
  if (filters.search.trim().length > 0) {
    query.set("search", filters.search.trim());
  }

  if (filters.status !== null) {
    query.set("status", filters.status);
  }

  if (filters.customerId !== null) {
    query.set("customerId", filters.customerId);
  }

  if (filters.unassigned) {
    query.set("unassigned", "true");
  } else if (filters.assignedUserId !== null) {
    query.set("assignedUserId", filters.assignedUserId);
  }

  if (filters.overdue) {
    query.set("overdue", "true");
  }

  query.set("sort", filters.sort);
  query.set("page", String(filters.page));

  return apiFetch<PagedResponse<TaskListItem>>(`${basePath(slug)}?${query.toString()}`, { signal });
}

export function getTask(slug: string, taskId: string, signal?: AbortSignal): Promise<TaskDetail> {
  return apiFetch<TaskDetail>(`${basePath(slug)}/${taskId}`, { signal });
}

export function createTask(slug: string, input: TaskInput): Promise<TaskDetail> {
  return apiFetch<TaskDetail>(basePath(slug), { method: "POST", body: input });
}

export function updateTask(slug: string, taskId: string, input: TaskInput): Promise<TaskDetail> {
  return apiFetch<TaskDetail>(`${basePath(slug)}/${taskId}`, { method: "PATCH", body: input });
}

/**
 * Moves the task between Todo, InProgress and Done.
 *
 * Its own route so ticking a checkbox in the list is one request; the full
 * update would need fields the list row does not carry.
 */
export function changeTaskStatus(
  slug: string,
  taskId: string,
  status: TaskStatus,
): Promise<TaskDetail> {
  return apiFetch<TaskDetail>(`${basePath(slug)}/${taskId}/status`, {
    method: "POST",
    body: { status },
  });
}

export function deleteTask(slug: string, taskId: string): Promise<void> {
  return apiFetch<void>(`${basePath(slug)}/${taskId}`, { method: "DELETE" });
}
