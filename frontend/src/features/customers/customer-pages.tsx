"use client";

import { WorkspaceShell } from "@/features/workspaces/workspace-shell";
import { CustomerDetailScreen } from "./customer-detail-screen";
import { CustomerListScreen } from "./customer-list-screen";

export function CustomerListPage({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="customers">
      {(workspace) => <CustomerListScreen workspace={workspace} />}
    </WorkspaceShell>
  );
}

export function CustomerDetailPage({
  workspaceSlug,
  customerId,
}: {
  workspaceSlug: string;
  customerId: string;
}) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="customers">
      {(workspace) => <CustomerDetailScreen workspace={workspace} customerId={customerId} />}
    </WorkspaceShell>
  );
}
