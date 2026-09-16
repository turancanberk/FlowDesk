import type { Metadata } from "next";
import { CustomerListPage } from "@/features/customers/customer-pages";

export const metadata: Metadata = {
  title: "Müşteriler",
};

export default async function WorkspaceCustomersPage({
  params,
}: PageProps<"/app/[workspaceSlug]/customers">) {
  const { workspaceSlug } = await params;

  return <CustomerListPage workspaceSlug={workspaceSlug} />;
}
