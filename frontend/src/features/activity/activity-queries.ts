"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { listActivity } from "./activity-api";
import type { ActivityFilters } from "./activity-types";

export const activityKeys = {
  all: (slug: string) => ["activity", slug] as const,
  list: (slug: string, filters: ActivityFilters) =>
    [...activityKeys.all(slug), "list", filters] as const,
};

export function useActivity(slug: string, filters: ActivityFilters) {
  return useQuery({
    queryKey: activityKeys.list(slug, filters),
    queryFn: ({ signal }) => listActivity(slug, filters, signal),
    // Keeps the current page on screen while the next one loads.
    placeholderData: keepPreviousData,
  });
}
