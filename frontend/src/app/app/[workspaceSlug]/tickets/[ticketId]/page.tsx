import type { Metadata } from "next";
import { TicketDetailPage } from "@/features/tickets/ticket-pages";

export const metadata: Metadata = {
  title: "Talep",
};

export default async function WorkspaceTicketDetailPage({
  params,
}: PageProps<"/app/[workspaceSlug]/tickets/[ticketId]">) {
  const { workspaceSlug, ticketId } = await params;

  return <TicketDetailPage workspaceSlug={workspaceSlug} ticketId={ticketId} />;
}
