"use client";

import { WorkspaceShell } from "@/features/workspaces/workspace-shell";
import { TicketDetailScreen } from "./ticket-detail-screen";
import { TicketListScreen } from "./ticket-list-screen";

export function TicketListPage({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="tickets">
      {(workspace) => <TicketListScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}

export function TicketDetailPage({
  workspaceSlug,
  ticketId,
}: {
  workspaceSlug: string;
  ticketId: string;
}) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="tickets">
      {(workspace) => <TicketDetailScreen workspace={workspace} ticketId={ticketId} />}
    </WorkspaceShell>
  );
}
