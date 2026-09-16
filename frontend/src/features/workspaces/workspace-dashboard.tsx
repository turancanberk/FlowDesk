"use client";

import { PageHeader } from "@/components/product/page-header";
import { WorkspaceShell } from "./workspace-shell";

/*
  Faz 04 dashboard'u.

  Gerçek operasyonel toplamlar Faz 09'da geliyor. Burada bilinçli olarak sahte
  KPI kartları gösterilmiyor: var olmayan sayıları ekrana koymak ilerleme
  izlenimi verir ama ilerleme değildir.
*/
export function WorkspaceDashboard({ workspaceSlug }: { workspaceSlug: string }) {
  return (
    <WorkspaceShell workspaceSlug={workspaceSlug} activeSection="dashboard">
      {() => (
        <div className="px-6 py-6">
          <PageHeader
            title="Dashboard"
            description="Çalışma alanınızın güncel durumu burada özetlenecek."
          />

          <p className="text-muted-foreground mt-6 text-sm">
            Müşteri, talep ve görev modülleri eklendikçe bu ekran gerçek operasyonel göstergelerle
            dolacak.
          </p>
        </div>
      )}
    </WorkspaceShell>
  );
}
