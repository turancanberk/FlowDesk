import { apiFetch } from "@/lib/api/http-client";
import type { Workspace } from "./workspace-types";

export function listWorkspaces(signal?: AbortSignal): Promise<Workspace[]> {
  return apiFetch<Workspace[]>("/api/workspaces", { signal });
}

export function getWorkspace(slug: string, signal?: AbortSignal): Promise<Workspace> {
  return apiFetch<Workspace>(`/api/workspaces/${encodeURIComponent(slug)}`, { signal });
}

export function createWorkspace(input: { name: string; slug?: string }): Promise<Workspace> {
  return apiFetch<Workspace>("/api/workspaces", { method: "POST", body: input });
}

export function updateWorkspace(slug: string, input: { name: string }): Promise<Workspace> {
  return apiFetch<Workspace>(`/api/workspaces/${encodeURIComponent(slug)}`, {
    method: "PATCH",
    body: input,
  });
}

export function deleteWorkspace(slug: string): Promise<void> {
  return apiFetch<void>(`/api/workspaces/${encodeURIComponent(slug)}`, { method: "DELETE" });
}
