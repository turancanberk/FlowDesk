"use client";

import { DashboardScreen } from "@/features/dashboard/dashboard-screen";
import { WorkspaceShell } from "./workspace-shell";

/**
 * The workspace's operational summary.
 *
 * Kept here rather than moved into the dashboard feature so that the route and
 * the shell wiring stay where the other workspace screens are; the screen
 * itself lives with the rest of its feature.
 */
export function WorkspaceDashboard({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="dashboard">
      {(workspace) => <DashboardScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}
