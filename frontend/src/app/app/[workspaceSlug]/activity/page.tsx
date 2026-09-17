import type { Metadata } from "next";
import { ActivityPage } from "@/features/activity/activity-page";

export const metadata: Metadata = {
  title: "Etkinlik",
};

export default async function WorkspaceActivityPage({
  params,
}: PageProps<"/app/[workspaceSlug]/activity">) {
  const { workspaceSlug } = await params;

  return <ActivityPage workspaceSlug={workspaceSlug} />;
}
