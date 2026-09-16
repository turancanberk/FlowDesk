import type { Metadata } from "next";
import { WorkspaceDashboard } from "@/features/workspaces/workspace-dashboard";

export const metadata: Metadata = {
  title: "Dashboard",
};

/*
  Server Component'ın işi burada kabuk kurmakla sınırlı: rota parametresini
  okuyup istemci bileşenine geçiriyor. Kimlik doğrulaması gerektiren veri
  çekme istemci tarafında yapılır, çünkü access token yalnızca tarayıcı
  belleğinde (ADR-0009).
*/
export default async function WorkspaceDashboardPage({
  params,
}: PageProps<"/app/[workspaceSlug]/dashboard">) {
  const { workspaceSlug } = await params;

  return <WorkspaceDashboard workspaceSlug={workspaceSlug} />;
}
