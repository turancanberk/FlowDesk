"use client";

import * as React from "react";
import { ListChecksIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/product/empty-state";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { TaskFormDialog } from "./task-form-dialog";
import { TaskList } from "./task-list";
import { useTask, useTasks } from "./task-queries";
import { INITIAL_TASK_FILTERS } from "./task-types";

/**
 * The tasks tied to one customer, shown on their detail page.
 *
 * Reuses the task list query with the customer filter applied, so the rows, the
 * paging and the empty states stay identical to the main list.
 */
export function CustomerTasksPanel({
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
  const [editingTaskId, setEditingTaskId] = React.useState<string | null>(null);

  const { data, isPending, isError, isPlaceholderData, refetch } = useTasks(workspace.slug, {
    ...INITIAL_TASK_FILTERS,
    customerId,
    page,
  });

  const editingTask = useTask(workspace.slug, editingTaskId ?? "");

  const dialogs = (
    <>
      <TaskFormDialog
        workspaceSlug={workspace.slug}
        fixedCustomerId={customerId}
        open={isCreating}
        onOpenChange={setIsCreating}
      />

      <TaskFormDialog
        workspaceSlug={workspace.slug}
        task={editingTask.data}
        open={editingTaskId !== null && editingTask.data !== undefined}
        onOpenChange={(open) => {
          if (!open) {
            setEditingTaskId(null);
          }
        }}
      />
    </>
  );

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
          title="Görevler yüklenemedi"
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
            icon={<ListChecksIcon />}
            title="Bu müşteriye bağlı görev yok"
            description="Bu müşteri için yapılacak işleri buradan takip edebilirsiniz."
            action={
              canManage ? (
                <Button
                  size="sm"
                  onClick={() => {
                    setIsCreating(true);
                  }}
                >
                  <PlusIcon />
                  Yeni görev
                </Button>
              ) : null
            }
          />
        </Surface>
        {dialogs}
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
            Yeni görev
          </Button>
        </div>
      ) : null}

      <TaskList
        workspaceSlug={workspace.slug}
        items={data.items}
        canManage={canManage}
        isStale={isPlaceholderData}
        // Every row would carry this customer's name, so the link is dropped.
        showCustomer={false}
        page={data.page}
        pageSize={data.pageSize}
        totalCount={data.totalCount}
        onPageChange={setPage}
        onEdit={(task) => {
          setEditingTaskId(task.id);
        }}
      />

      {dialogs}
    </>
  );
}

function Surface({ children }: { children: React.ReactNode }) {
  return <div className="border-border bg-card overflow-hidden rounded-xl border">{children}</div>;
}
