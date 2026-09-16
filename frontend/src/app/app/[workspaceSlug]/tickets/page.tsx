import type { Metadata } from "next";
import { TicketListPage } from "@/features/tickets/ticket-pages";

export const metadata: Metadata = {
  title: "Talepler",
};

export default async function WorkspaceTicketsPage({
  params,
}: PageProps<"/app/[workspaceSlug]/tickets">) {
  const { workspaceSlug } = await params;

  return <TicketListPage workspaceSlug={workspaceSlug} />;
}
