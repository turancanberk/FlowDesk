import { apiFetch } from "@/lib/api/http-client";
import type { NotificationFeed } from "./notification-types";

const basePath = (slug: string) => `/api/workspaces/${encodeURIComponent(slug)}/notifications`;

export function listNotifications(slug: string, signal?: AbortSignal): Promise<NotificationFeed> {
  return apiFetch<NotificationFeed>(basePath(slug), { signal });
}

/** An empty list marks everything read. */
export function markNotificationsRead(slug: string, notificationIds: string[]): Promise<void> {
  return apiFetch<void>(`${basePath(slug)}/read`, {
    method: "POST",
    body: { notificationIds },
  });
}
