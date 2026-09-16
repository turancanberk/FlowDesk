import type { Metadata } from "next";
import { CustomerDetailPage } from "@/features/customers/customer-pages";

export const metadata: Metadata = {
  title: "Müşteri",
};

export default async function WorkspaceCustomerDetailPage({
  params,
}: PageProps<"/app/[workspaceSlug]/customers/[customerId]">) {
  const { workspaceSlug, customerId } = await params;

  return <CustomerDetailPage workspaceSlug={workspaceSlug} customerId={customerId} />;
}
