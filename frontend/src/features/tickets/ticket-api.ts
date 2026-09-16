import { apiFetch } from "@/lib/api/http-client";
import type { TicketStatus } from "@/types/domain";
import type {
  CreateTicketInput,
  TicketComment,
  TicketDetail,
  TicketFilters,
  TicketListItem,
  UpdateTicketInput,
} from "./ticket-types";
import type { PagedResponse } from "@/features/customers/customer-types";

const basePath = (slug: string) => `/api/workspaces/${encodeURIComponent(slug)}/tickets`;

export function listTickets(
  slug: string,
  filters: TicketFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<TicketListItem>> {
  const query = new URLSearchParams();

  // Only non-default values are sent, which keeps the request URL — and the
  // query cache key derived from it — readable.
  if (filters.search.trim().length > 0) {
    query.set("search", filters.search.trim());
  }

  if (filters.status !== null) {
    query.set("status", filters.status);
  }

  if (filters.priority !== null) {
    query.set("priority", filters.priority);
  }

  if (filters.customerId !== null) {
    query.set("customerId", filters.customerId);
  }

  if (filters.unassigned) {
    query.set("unassigned", "true");
  } else if (filters.assignedUserId !== null) {
    query.set("assignedUserId", filters.assignedUserId);
  }

  query.set("sort", filters.sort);
  query.set("page", String(filters.page));

  return apiFetch<PagedResponse<TicketListItem>>(`${basePath(slug)}?${query.toString()}`, {
    signal,
  });
}

export function getTicket(
  slug: string,
  ticketId: string,
  signal?: AbortSignal,
): Promise<TicketDetail> {
  return apiFetch<TicketDetail>(`${basePath(slug)}/${ticketId}`, { signal });
}

export function createTicket(slug: string, input: CreateTicketInput): Promise<TicketDetail> {
  return apiFetch<TicketDetail>(basePath(slug), { method: "POST", body: input });
}

export function updateTicket(
  slug: string,
  ticketId: string,
  input: UpdateTicketInput,
): Promise<TicketDetail> {
  return apiFetch<TicketDetail>(`${basePath(slug)}/${ticketId}`, { method: "PATCH", body: input });
}

/**
 * Moves the ticket to another status.
 *
 * A state transition rather than a field edit, which is why it has its own
 * route: whether it is allowed depends on where the ticket currently stands.
 */
export function changeTicketStatus(
  slug: string,
  ticketId: string,
  status: TicketStatus,
): Promise<TicketDetail> {
  return apiFetch<TicketDetail>(`${basePath(slug)}/${ticketId}/status`, {
    method: "POST",
    body: { status },
  });
}

/** Passing null hands the ticket back to the unassigned queue. */
export function assignTicket(
  slug: string,
  ticketId: string,
  assignedUserId: string | null,
): Promise<TicketDetail> {
  return apiFetch<TicketDetail>(`${basePath(slug)}/${ticketId}/assignment`, {
    method: "POST",
    body: { assignedUserId },
  });
}

export function deleteTicket(slug: string, ticketId: string): Promise<void> {
  return apiFetch<void>(`${basePath(slug)}/${ticketId}`, { method: "DELETE" });
}

export function listTicketComments(
  slug: string,
  ticketId: string,
  signal?: AbortSignal,
): Promise<TicketComment[]> {
  return apiFetch<TicketComment[]>(`${basePath(slug)}/${ticketId}/comments`, { signal });
}

export function addTicketComment(
  slug: string,
  ticketId: string,
  body: string,
): Promise<TicketComment> {
  return apiFetch<TicketComment>(`${basePath(slug)}/${ticketId}/comments`, {
    method: "POST",
    body: { body },
  });
}
