import type { Metadata } from "next";
import { TeamPage } from "@/features/team/team-page";

export const metadata: Metadata = {
  title: "Ekip",
};

export default async function WorkspaceTeamPage({
  params,
}: PageProps<"/app/[workspaceSlug]/team">) {
  const { workspaceSlug } = await params;

  return <TeamPage workspaceSlug={workspaceSlug} />;
}
