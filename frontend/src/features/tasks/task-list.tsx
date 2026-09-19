"use client";

import * as React from "react";
import Link from "next/link";
import { MoreHorizontalIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Pagination } from "@/components/ui/pagination";
import { StatusBadge, TaskStatusBadge } from "@/components/product/status-badge";
import { formatDate } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import { useChangeTaskStatus, useDeleteTask } from "./task-queries";
import type { TaskListItem } from "./task-types";

/**
 * The task list, shared by the tasks page and a customer's task tab.
 *
 * Rows rather than a table: a task is a title, a date and an owner, and the
 * thing people do most is tick it off. A checkbox at the start of each row
 * makes that one click; a data table would put it behind a menu.
 */
export function TaskList({
  workspaceSlug,
  items,
  canManage,
  isStale,
  showCustomer = true,
  page,
  pageSize,
  totalCount,
  onPageChange,
  onEdit,
}: {
  workspaceSlug: string;
  items: TaskListItem[];
  canManage: boolean;
  isStale: boolean;
  showCustomer?: boolean;
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onEdit: (task: TaskListItem) => void;
}) {
  const statusMutation = useChangeTaskStatus(workspaceSlug);

  return (
    <div
      className="border-border bg-card overflow-hidden rounded-xl border transition-opacity duration-100"
      // Dims while the next page loads rather than swapping in a skeleton.
      style={{ opacity: isStale ? 0.6 : 1 }}
    >
      <ul className="divide-border divide-y">
        {items.map((task) => (
          <li key={task.id} className="hover:bg-muted/50 flex items-center gap-3 px-4 py-2.5">
            <Checkbox
              checked={task.status === "Done"}
              disabled={!canManage || statusMutation.isPending}
              aria-label={`${task.title} tamamlandı olarak işaretle`}
              onCheckedChange={(checked) => {
                statusMutation.mutate(
                  { taskId: task.id, status: checked === true ? "Done" : "Todo" },
                  {
                    onError: (error) => {
                      toast.error("Görev durumu değiştirilemedi", {
                        description:
                          error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                      });
                    },
                  },
                );
              }}
            />

            <div className="min-w-0 flex-1">
              <span
                className={
                  task.status === "Done"
                    ? "text-muted-foreground block truncate text-sm line-through"
                    : "text-foreground block truncate text-sm font-medium"
                }
              >
                {task.title}
              </span>

              {showCustomer && task.customerId !== null ? (
                <Link
                  href={`/app/${workspaceSlug}/customers/${task.customerId}`}
                  className="text-muted-foreground hover:text-foreground block truncate rounded-sm text-xs transition-colors duration-100"
                >
                  {task.customerName}
                </Link>
              ) : null}
            </div>

            <span className="text-muted-foreground hidden w-32 shrink-0 truncate text-xs sm:block">
              {task.assignedUserDisplayName ?? "Atanmamış"}
            </span>

            {/*
              Overdue is its own badge rather than red text on the date: colour
              alone carries no meaning for someone who cannot see it
              (docs/DESIGN_SYSTEM.md).
            */}
            <span className="w-28 shrink-0 text-right">
              {task.dueAt === null ? (
                <span className="text-muted-foreground text-xs">—</span>
              ) : task.isOverdue ? (
                <StatusBadge tone="danger">{formatDate(task.dueAt)}</StatusBadge>
              ) : (
                <span className="text-muted-foreground text-xs">{formatDate(task.dueAt)}</span>
              )}
            </span>

            <span className="w-24 shrink-0 text-right">
              <TaskStatusBadge status={task.status} />
            </span>

            {canManage ? (
              <TaskRowActions
                workspaceSlug={workspaceSlug}
                task={task}
                onEdit={() => {
                  onEdit(task);
                }}
              />
            ) : null}
          </li>
        ))}
      </ul>

      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={onPageChange}
      />
    </div>
  );
}

function TaskRowActions({
  workspaceSlug,
  task,
  onEdit,
}: {
  workspaceSlug: string;
  task: TaskListItem;
  onEdit: () => void;
}) {
  const deleteMutation = useDeleteTask(workspaceSlug);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon-sm" aria-label={`${task.title} için işlemler`}>
            <MoreHorizontalIcon />
          </Button>
        }
      />

      <DropdownMenuContent align="end" className="w-44">
        <DropdownMenuItem onClick={onEdit}>
          <PencilIcon />
          Düzenle
        </DropdownMenuItem>

        <DropdownMenuItem
          variant="destructive"
          disabled={deleteMutation.isPending}
          onClick={() => {
            deleteMutation.mutate(task.id, {
              onSuccess: () => {
                toast.success("Görev silindi", { description: task.title });
              },
              onError: (error) => {
                toast.error("Görev silinemedi", {
                  description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                });
              },
            });
          }}
        >
          <Trash2Icon />
          Sil
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
