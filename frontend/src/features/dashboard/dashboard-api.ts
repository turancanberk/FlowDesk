import { apiFetch } from "@/lib/api/http-client";
import type { DashboardSummary } from "./dashboard-types";

export function getDashboard(slug: string, signal?: AbortSignal): Promise<DashboardSummary> {
  return apiFetch<DashboardSummary>(
    `/api/workspaces/${encodeURIComponent(slug)}/dashboard`,
    { signal },
  );
}
