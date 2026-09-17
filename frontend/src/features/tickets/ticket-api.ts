import { apiDownload, apiFetch } from "@/lib/api/http-client";
import type { TicketStatus } from "@/types/domain";
import type {
  CreateTicketInput,
  TicketAttachment,
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

export function listAttachments(
  slug: string,
  ticketId: string,
  signal?: AbortSignal,
): Promise<TicketAttachment[]> {
  return apiFetch<TicketAttachment[]>(`${basePath(slug)}/${ticketId}/attachments`, { signal });
}

/**
 * Uploads a file.
 *
 * Sent as multipart because that is what a file is. The field name matches the
 * parameter the endpoint binds, so renaming one without the other produces a
 * request the server reads as having no file at all.
 */
export function uploadAttachment(
  slug: string,
  ticketId: string,
  file: File,
): Promise<TicketAttachment> {
  const formData = new FormData();
  formData.append("file", file, file.name);

  return apiFetch<TicketAttachment>(`${basePath(slug)}/${ticketId}/attachments`, {
    method: "POST",
    formData,
  });
}

/**
 * Fetches an attachment's content.
 *
 * Through the API rather than a direct link: the bytes are private and the
 * request has to carry the bearer token.
 */
export function downloadAttachment(
  slug: string,
  ticketId: string,
  attachmentId: string,
): Promise<{ blob: Blob; fileName: string | null }> {
  return apiDownload(`${basePath(slug)}/${ticketId}/attachments/${attachmentId}`);
}

export function deleteAttachment(
  slug: string,
  ticketId: string,
  attachmentId: string,
): Promise<void> {
  return apiFetch<void>(`${basePath(slug)}/${ticketId}/attachments/${attachmentId}`, {
    method: "DELETE",
  });
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
