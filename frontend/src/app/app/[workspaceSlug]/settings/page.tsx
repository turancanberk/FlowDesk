import type { Metadata } from "next";
import { WorkspaceSettingsPage } from "@/features/workspaces/workspace-settings-page";

export const metadata: Metadata = {
  title: "Ayarlar",
};

export default async function WorkspaceSettingsRoute({
  params,
}: PageProps<"/app/[workspaceSlug]/settings">) {
  const { workspaceSlug } = await params;

  return <WorkspaceSettingsPage workspaceSlug={workspaceSlug} />;
}
