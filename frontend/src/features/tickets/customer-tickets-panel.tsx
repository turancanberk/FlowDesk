"use client";

import * as React from "react";
import { PlusIcon, TicketIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/product/empty-state";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { TicketFormDialog } from "./ticket-form-dialog";
import { useTickets } from "./ticket-queries";
import { TicketTable } from "./ticket-table";
import { INITIAL_TICKET_FILTERS } from "./ticket-types";

/**
 * The tickets raised for one customer, shown on their detail page.
 *
 * Reuses the ticket list query with the customer filter applied, so the columns,
 * the paging and the empty states stay identical to the main list.
 */
export function CustomerTicketsPanel({
  workspace,
  customerId,
  canManage,
}: {
  workspace: Workspace;
  customerId: string;
  canManage: boolean;
}) {
  const [page, setPage] = React.useState(1);
  const [isCreating, setIsCreating] = React.useState(false);

  const { data, isPending, isError, isPlaceholderData, refetch } = useTickets(workspace.slug, {
    ...INITIAL_TICKET_FILTERS,
    customerId,
    page,
  });

  if (isPending) {
    return (
      <div className="border-border bg-card flex flex-col gap-3 rounded-xl border p-4">
        {[0, 1, 2].map((row) => (
          <Skeleton key={row} className="h-4" />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <Surface>
        <EmptyState
          title="Talepler yüklenemedi"
          description="Bağlantı kurulamadı. Tekrar denemek sorunu çözebilir."
          action={
            <Button
              size="sm"
              variant="outline"
              onClick={() => {
                void refetch();
              }}
            >
              Tekrar dene
            </Button>
          }
        />
      </Surface>
    );
  }

  if (data.items.length === 0) {
    return (
      <>
        <Surface>
          <EmptyState
            icon={<TicketIcon />}
            title="Bu müşteriye açılmış talep yok"
            description="Müşteriden gelen ilk destek talebini buradan açabilirsiniz."
            action={
              canManage ? (
                <Button
                  size="sm"
                  onClick={() => {
                    setIsCreating(true);
                  }}
                >
                  <PlusIcon />
                  Yeni talep
                </Button>
              ) : null
            }
          />
        </Surface>

        <TicketFormDialog
          workspaceSlug={workspace.slug}
          fixedCustomerId={customerId}
          open={isCreating}
          onOpenChange={setIsCreating}
        />
      </>
    );
  }

  return (
    <>
      {canManage ? (
        <div className="flex justify-end">
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              setIsCreating(true);
            }}
          >
            <PlusIcon />
            Yeni talep
          </Button>
        </div>
      ) : null}

      <TicketTable
        workspaceSlug={workspace.slug}
        items={data.items}
        isStale={isPlaceholderData}
        // Every row would carry this customer's name, so the column is dropped
        // and the space goes to the subject.
        showCustomer={false}
        page={data.page}
        pageSize={data.pageSize}
        totalCount={data.totalCount}
        onPageChange={setPage}
      />

      <TicketFormDialog
        workspaceSlug={workspace.slug}
        fixedCustomerId={customerId}
        open={isCreating}
        onOpenChange={setIsCreating}
      />
    </>
  );
}

function Surface({ children }: { children: React.ReactNode }) {
  return <div className="border-border bg-card overflow-hidden rounded-xl border">{children}</div>;
}
