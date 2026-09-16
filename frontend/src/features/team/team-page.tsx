"use client";

import { WorkspaceShell } from "@/features/workspaces/workspace-shell";
import { TeamScreen } from "./team-screen";

export function TeamPage({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="team">
      {(workspace) => <TeamScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}
