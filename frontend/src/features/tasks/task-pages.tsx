"use client";

import { WorkspaceShell } from "@/features/workspaces/workspace-shell";
import { TaskListScreen } from "./task-list-screen";

export function TaskListPage({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="tasks">
      {(workspace) => <TaskListScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}
