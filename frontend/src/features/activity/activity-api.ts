import { apiFetch } from "@/lib/api/http-client";
import type { PagedResponse } from "@/features/customers/customer-types";
import type { ActivityFilters, ActivityItem } from "./activity-types";

export function listActivity(
  slug: string,
  filters: ActivityFilters,
  signal?: AbortSignal,
): Promise<PagedResponse<ActivityItem>> {
  const query = new URLSearchParams();

  if (filters.type !== null) {
    query.set("type", filters.type);
  }

  if (filters.subjectType !== null) {
    query.set("subjectType", filters.subjectType);
  }

  query.set("page", String(filters.page));

  return apiFetch<PagedResponse<ActivityItem>>(
    `/api/workspaces/${encodeURIComponent(slug)}/activity?${query.toString()}`,
    { signal },
  );
}
