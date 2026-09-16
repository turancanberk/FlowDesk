import type { Metadata } from "next";
import { WorkspaceList } from "@/features/workspaces/workspace-list";

export const metadata: Metadata = {
  title: "Çalışma alanları",
};

export default function HomePage() {
  return <WorkspaceList />;
}
