import type { Metadata } from "next";
import { NewWorkspaceScreen } from "@/features/workspaces/new-workspace-screen";

export const metadata: Metadata = {
  title: "Yeni çalışma alanı",
};

export default function NewWorkspacePage() {
  return <NewWorkspaceScreen />;
}
