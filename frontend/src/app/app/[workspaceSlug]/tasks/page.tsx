import type { Metadata } from "next";
import { TaskListPage } from "@/features/tasks/task-pages";

export const metadata: Metadata = {
  title: "Görevler",
};

export default async function WorkspaceTasksPage({
  params,
}: PageProps<"/app/[workspaceSlug]/tasks">) {
  const { workspaceSlug } = await params;

  return <TaskListPage workspaceSlug={workspaceSlug} />;
}
