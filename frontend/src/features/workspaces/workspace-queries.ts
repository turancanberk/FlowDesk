"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createWorkspace,
  deleteWorkspace,
  getWorkspace,
  listWorkspaces,
  updateWorkspace,
} from "./workspace-api";
import type { Workspace } from "./workspace-types";

export const workspaceKeys = {
  all: ["workspaces"] as const,
  list: () => [...workspaceKeys.all, "list"] as const,
  detail: (slug: string) => [...workspaceKeys.all, "detail", slug] as const,
};

export function useWorkspaces() {
  return useQuery({
    queryKey: workspaceKeys.list(),
    queryFn: ({ signal }) => listWorkspaces(signal),
  });
}

export function useWorkspace(slug: string) {
  return useQuery({
    queryKey: workspaceKeys.detail(slug),
    queryFn: ({ signal }) => getWorkspace(slug, signal),
    // A slug that does not resolve is an answer, not a transient failure.
    retry: false,
  });
}

export function useCreateWorkspace() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: createWorkspace,
    onSuccess: (workspace: Workspace) => {
      queryClient.setQueryData(workspaceKeys.detail(workspace.slug), workspace);
      void queryClient.invalidateQueries({ queryKey: workspaceKeys.list() });
    },
  });
}

export function useUpdateWorkspace(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { name: string }) => updateWorkspace(slug, input),
    onSuccess: (workspace: Workspace) => {
      queryClient.setQueryData(workspaceKeys.detail(slug), workspace);
      void queryClient.invalidateQueries({ queryKey: workspaceKeys.list() });
    },
  });
}

export function useDeleteWorkspace(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => deleteWorkspace(slug),
    onSuccess: () => {
      // Remove rather than invalidate: refetching a workspace that no longer
      // exists would produce a 404 the user has no way to act on.
      queryClient.removeQueries({ queryKey: workspaceKeys.detail(slug) });
      void queryClient.invalidateQueries({ queryKey: workspaceKeys.list() });
    },
  });
}
