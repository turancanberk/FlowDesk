"use client";

import * as React from "react";
import { ListChecksIcon, PlusIcon, SearchIcon, SearchXIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/product/empty-state";
import { PageHeader } from "@/components/product/page-header";
import { taskSortLabels, taskStatusLabels } from "@/lib/domain-labels";
import { TASK_STATUSES, type TaskStatus } from "@/types/domain";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { useCurrentUser } from "@/features/auth/auth-queries";
import { TaskFormDialog } from "./task-form-dialog";
import { TaskList } from "./task-list";
import { useTask, useTasks } from "./task-queries";
import { INITIAL_TASK_FILTERS, type TaskFilters, type TaskSort } from "./task-types";

/** The assignment filter, as one control rather than two unrelated ones. */
const ALL_ASSIGNEES = "all";
const MINE = "mine";
const UNASSIGNED = "unassigned";

export function TaskListScreen({ workspace }: { workspace: Workspace }) {
  const [filters, setFilters] = React.useState<TaskFilters>(INITIAL_TASK_FILTERS);
  const [searchInput, setSearchInput] = React.useState("");
  const [editingTaskId, setEditingTaskId] = React.useState<string | null>(null);
  const [isCreating, setIsCreating] = React.useState(false);

  const currentUser = useCurrentUser();
  const canManage = workspace.role !== "Viewer";

  React.useEffect(() => {
    const timer = setTimeout(() => {
      setFilters((current) =>
        current.search === searchInput ? current : { ...current, search: searchInput, page: 1 },
      );
    }, 250);

    return () => {
      clearTimeout(timer);
    };
  }, [searchInput]);

  const { data, isPending, isError, isPlaceholderData, refetch } = useTasks(
    workspace.slug,
    filters,
  );

  /*
    The edit dialog needs the description, which the list row does not carry, so
    the full task is fetched once a row is chosen. Nothing is requested until
    then.
  */
  const editingTask = useTask(workspace.slug, editingTaskId ?? "");

  const hasActiveFilters =
    filters.search.trim().length > 0 ||
    filters.status !== null ||
    filters.assignedUserId !== null ||
    filters.unassigned ||
    filters.overdue;

  return (
    <div className="px-6 py-6">
      <PageHeader
        title="Görevler"
        description="Ekibin takip ettiği işler ve son tarihleri."
        actions={
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

      <TaskFilterBar
        filters={filters}
        searchInput={searchInput}
        currentUserId={currentUser.data?.id ?? null}
        onSearchChange={setSearchInput}
        onFiltersChange={setFilters}
      />

      <div className="mt-5">
        {isPending ? (
          <ListSkeleton />
        ) : isError ? (
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
        ) : data.items.length === 0 ? (
          <Surface>
            {hasActiveFilters ? (
              <EmptyState
                icon={<SearchXIcon />}
                title="Filtreye uyan görev yok"
                description="Arama terimini veya seçtiğiniz filtreleri değiştirmeyi deneyin."
                action={
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setSearchInput("");
                      setFilters(INITIAL_TASK_FILTERS);
                    }}
                  >
                    Filtreleri temizle
                  </Button>
                }
              />
            ) : (
              <EmptyState
                icon={<ListChecksIcon />}
                title="Henüz görev yok"
                description="Ekibin takip etmesi gereken ilk işi ekleyin."
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
            )}
          </Surface>
        ) : (
          <TaskList
            workspaceSlug={workspace.slug}
            items={data.items}
            canManage={canManage}
            isStale={isPlaceholderData}
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            onPageChange={(page) => {
              setFilters((current) => ({ ...current, page }));
            }}
            onEdit={(task) => {
              setEditingTaskId(task.id);
            }}
          />
        )}
      </div>

      <TaskFormDialog
        workspaceSlug={workspace.slug}
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
    </div>
  );
}

function TaskFilterBar({
  filters,
  searchInput,
  currentUserId,
  onSearchChange,
  onFiltersChange,
}: {
  filters: TaskFilters;
  searchInput: string;
  currentUserId: string | null;
  onSearchChange: (value: string) => void;
  onFiltersChange: React.Dispatch<React.SetStateAction<TaskFilters>>;
}) {
  const statusOptions: Record<string, string> = {
    all: "Tüm durumlar",
    ...Object.fromEntries(TASK_STATUSES.map((status) => [status, taskStatusLabels[status].label])),
  };

  const assignmentOptions: Record<string, string> = {
    [ALL_ASSIGNEES]: "Tüm atamalar",
    [MINE]: "Bana atananlar",
    [UNASSIGNED]: "Atanmamışlar",
  };

  const assignment = filters.unassigned
    ? UNASSIGNED
    : filters.assignedUserId === null
      ? ALL_ASSIGNEES
      : MINE;

  return (
    <div className="mt-5 flex flex-wrap items-center gap-3">
      <div className="relative min-w-56 flex-1">
        <SearchIcon
          className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2"
          aria-hidden="true"
        />
        <Input
          value={searchInput}
          onChange={(event) => {
            onSearchChange(event.target.value);
          }}
          placeholder="Görev başlığı ara"
          aria-label="Görev ara"
          className="pl-8"
        />
      </div>

      <Select
        items={statusOptions}
        value={filters.status ?? "all"}
        onValueChange={(value) => {
          onFiltersChange((current) => ({
            ...current,
            status: value === "all" ? null : (value as TaskStatus),
            page: 1,
          }));
        }}
      >
        <SelectTrigger className="w-36" aria-label="Duruma göre filtrele">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(statusOptions).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        items={assignmentOptions}
        value={assignment}
        onValueChange={(value) => {
          onFiltersChange((current) => ({
            ...current,
            unassigned: value === UNASSIGNED,
            assignedUserId: value === MINE ? currentUserId : null,
            page: 1,
          }));
        }}
      >
        <SelectTrigger className="w-40" aria-label="Atamaya göre filtrele">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(assignmentOptions).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        items={taskSortLabels}
        value={filters.sort}
        onValueChange={(value) => {
          onFiltersChange((current) => ({ ...current, sort: value as TaskSort, page: 1 }));
        }}
      >
        <SelectTrigger className="w-44" aria-label="Sıralama">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(taskSortLabels).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {/*
        Overdue gets a control of its own rather than a place in a dropdown. It
        is the one question a task list is opened to answer, and burying it
        among four other options would cost a click every time.
      */}
      <div className="flex items-center gap-2">
        <Checkbox
          id="overdue-only"
          checked={filters.overdue}
          onCheckedChange={(checked) => {
            onFiltersChange((current) => ({ ...current, overdue: checked === true, page: 1 }));
          }}
        />
        <Label htmlFor="overdue-only" className="font-normal">
          Yalnızca gecikenler
        </Label>
      </div>
    </div>
  );
}

function Surface({ children }: { children: React.ReactNode }) {
  return <div className="border-border bg-card overflow-hidden rounded-xl border">{children}</div>;
}

function ListSkeleton() {
  return (
    <div className="border-border bg-card flex flex-col gap-3 rounded-xl border p-4">
      {[0, 1, 2, 3, 4].map((row) => (
        <div key={row} className="flex items-center gap-4">
          <Skeleton className="size-4 rounded-sm" />
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-20" />
        </div>
      ))}
    </div>
  );
}
