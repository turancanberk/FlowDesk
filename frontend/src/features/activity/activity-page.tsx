"use client";

import { WorkspaceShell } from "@/features/workspaces/workspace-shell";
import { ActivityScreen } from "./activity-screen";

export function ActivityPage({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="activity">
      {(workspace) => <ActivityScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}
