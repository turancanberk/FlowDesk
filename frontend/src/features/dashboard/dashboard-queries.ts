"use client";

import { useQuery } from "@tanstack/react-query";
import { getDashboard } from "./dashboard-api";

export const dashboardKeys = {
  summary: (slug: string) => ["dashboard", slug] as const,
};

/**
 * The workspace's operational figures.
 *
 * Kept fresh for a minute rather than refetched on every focus: these are
 * counts a team glances at, and a number that changes while being read is
 * worse than one that is a minute old. The screen says when it was taken.
 */
export function useDashboard(slug: string) {
  return useQuery({
    queryKey: dashboardKeys.summary(slug),
    queryFn: ({ signal }) => getDashboard(slug, signal),
    staleTime: 60 * 1000,
  });
}
