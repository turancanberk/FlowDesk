"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { listNotifications, markNotificationsRead } from "./notification-api";

export const notificationKeys = {
  feed: (slug: string) => ["notifications", slug] as const,
};

/**
 * The caller's notifications in this workspace.
 *
 * Polled rather than pushed. Notifications arrive through a worker, so nothing
 * tells the browser when one lands; a minute's delay on a badge is not worth a
 * WebSocket and the connection management that comes with it (docs/ROADMAP.md).
 */
export function useNotifications(slug: string) {
  return useQuery({
    queryKey: notificationKeys.feed(slug),
    queryFn: ({ signal }) => listNotifications(slug, signal),
    refetchInterval: 60 * 1000,
    // A tab left open in the background does not need to keep asking.
    refetchIntervalInBackground: false,
  });
}

export function useMarkNotificationsRead(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (notificationIds: string[]) => markNotificationsRead(slug, notificationIds),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: notificationKeys.feed(slug) });
    },
  });
}
