import { apiFetch } from "@/lib/api/http-client";
import type {
  CustomerDetail,
  CustomerFilters,
  CustomerInput,
  CustomerListItem,
  PagedResponse,
} from "./customer-types";

const basePath = (slug: string) => `/api/workspaces/${encodeURIComponent(slug)}/customers`;

export function listCustomers(
  slug: string,
  filters: CustomerFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<CustomerListItem>> {
  const query = new URLSearchParams();

  // Only non-default values are sent, which keeps the request URL — and the
  // query cache key derived from it — readable.
  if (filters.search.trim().length > 0) {
    query.set("search", filters.search.trim());
  }

  if (filters.status !== null) {
    query.set("status", filters.status);
  }

  if (filters.includeArchived) {
    query.set("includeArchived", "true");
  }

  query.set("sort", filters.sort);
  query.set("page", String(filters.page));

  return apiFetch<PagedResponse<CustomerListItem>>(`${basePath(slug)}?${query.toString()}`, {
    signal,
  });
}

export function getCustomer(
  slug: string,
  customerId: string,
  signal?: AbortSignal,
): Promise<CustomerDetail> {
  return apiFetch<CustomerDetail>(`${basePath(slug)}/${customerId}`, { signal });
}

export function createCustomer(slug: string, input: CustomerInput): Promise<CustomerDetail> {
  return apiFetch<CustomerDetail>(basePath(slug), { method: "POST", body: input });
}

export function updateCustomer(
  slug: string,
  customerId: string,
  input: CustomerInput,
): Promise<CustomerDetail> {
  return apiFetch<CustomerDetail>(`${basePath(slug)}/${customerId}`, {
    method: "PATCH",
    body: input,
  });
}

/** Archives rather than deletes; the record stays for its tickets and tasks. */
export function archiveCustomer(slug: string, customerId: string): Promise<void> {
  return apiFetch<void>(`${basePath(slug)}/${customerId}`, { method: "DELETE" });
}

export function restoreCustomer(slug: string, customerId: string): Promise<void> {
  return apiFetch<void>(`${basePath(slug)}/${customerId}/restore`, { method: "POST" });
}
