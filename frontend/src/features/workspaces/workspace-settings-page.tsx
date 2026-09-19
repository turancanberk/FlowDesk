"use client";

import { WorkspaceShell } from "./workspace-shell";
import { WorkspaceSettingsScreen } from "./workspace-settings-screen";

export function WorkspaceSettingsPage({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="settings">
      {(workspace) => <WorkspaceSettingsScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}
