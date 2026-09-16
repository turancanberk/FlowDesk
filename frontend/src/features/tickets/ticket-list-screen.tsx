"use client";

import * as React from "react";
import { PlusIcon, SearchIcon, SearchXIcon, TicketIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
import { ticketPriorityLabels, ticketSortLabels, ticketStatusLabels } from "@/lib/domain-labels";
import { TICKET_PRIORITIES, TICKET_STATUSES } from "@/types/domain";
import type { TicketPriority, TicketStatus } from "@/types/domain";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { useCurrentUser } from "@/features/auth/auth-queries";
import { TicketFormDialog } from "./ticket-form-dialog";
import { useTickets } from "./ticket-queries";
import { TicketTable } from "./ticket-table";
import { INITIAL_TICKET_FILTERS, type TicketFilters, type TicketSort } from "./ticket-types";

/** The assignment filter, as one control rather than two unrelated ones. */
const ALL_ASSIGNEES = "all";
const MINE = "mine";
const UNASSIGNED = "unassigned";

export function TicketListScreen({ workspace }: { workspace: Workspace }) {
  const [filters, setFilters] = React.useState<TicketFilters>(INITIAL_TICKET_FILTERS);
  const [searchInput, setSearchInput] = React.useState("");
  const [isCreating, setIsCreating] = React.useState(false);

  const currentUser = useCurrentUser();
  const canManage = workspace.role !== "Viewer";

  /*
    Debounced so typing does not fire a request per keystroke. The input keeps
    its own state and the committed value lands in the filters, which is what
    the query key is built from.
  */
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

  const { data, isPending, isError, isPlaceholderData, refetch } = useTickets(
    workspace.slug,
    filters,
  );

  const hasActiveFilters =
    filters.search.trim().length > 0 ||
    filters.status !== null ||
    filters.priority !== null ||
    filters.assignedUserId !== null ||
    filters.unassigned;

  return (
    <div className="px-6 py-6">
      <PageHeader
        title="Talepler"
        description="Müşterilerinizden gelen destek talepleri."
        actions={
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

      <TicketFilterBar
        filters={filters}
        searchInput={searchInput}
        currentUserId={currentUser.data?.id ?? null}
        onSearchChange={setSearchInput}
        onFiltersChange={setFilters}
      />

      {isPending ? (
        <TableSkeleton />
      ) : isError ? (
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
      ) : data.items.length === 0 ? (
        <Surface>
          {hasActiveFilters ? (
            <EmptyState
              icon={<SearchXIcon />}
              title="Filtreye uyan talep yok"
              description="Arama terimini veya seçtiğiniz filtreleri değiştirmeyi deneyin."
              action={
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setSearchInput("");
                    setFilters(INITIAL_TICKET_FILTERS);
                  }}
                >
                  Filtreleri temizle
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={<TicketIcon />}
              title="Henüz talep yok"
              description="Bir müşteri adına ilk talebi açtığınızda burada listelenecek."
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
          )}
        </Surface>
      ) : (
        <TicketTable
          workspaceSlug={workspace.slug}
          items={data.items}
          isStale={isPlaceholderData}
          page={data.page}
          pageSize={data.pageSize}
          totalCount={data.totalCount}
          onPageChange={(page) => {
            setFilters((current) => ({ ...current, page }));
          }}
        />
      )}

      <TicketFormDialog
        workspaceSlug={workspace.slug}
        open={isCreating}
        onOpenChange={setIsCreating}
      />
    </div>
  );
}

function TicketFilterBar({
  filters,
  searchInput,
  currentUserId,
  onSearchChange,
  onFiltersChange,
}: {
  filters: TicketFilters;
  searchInput: string;
  currentUserId: string | null;
  onSearchChange: (value: string) => void;
  onFiltersChange: React.Dispatch<React.SetStateAction<TicketFilters>>;
}) {
  const statusOptions: Record<string, string> = {
    all: "Tüm durumlar",
    ...Object.fromEntries(
      TICKET_STATUSES.map((status) => [status, ticketStatusLabels[status].label]),
    ),
  };

  const priorityOptions: Record<string, string> = {
    all: "Tüm öncelikler",
    ...Object.fromEntries(
      TICKET_PRIORITIES.map((priority) => [priority, ticketPriorityLabels[priority].label]),
    ),
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
          // A number typed here finds that ticket, which is how teams actually
          // look one up.
          placeholder="Konu veya talep numarası ara"
          aria-label="Talep ara"
          className="pl-8"
        />
      </div>

      {/*
        Base UI resolves the trigger label from `items`; without it the trigger
        shows the raw value, which would put English codes like "InProgress"
        into a Turkish interface.
      */}
      <Select
        items={statusOptions}
        value={filters.status ?? "all"}
        onValueChange={(value) => {
          onFiltersChange((current) => ({
            ...current,
            status: value === "all" ? null : (value as TicketStatus),
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
        items={priorityOptions}
        value={filters.priority ?? "all"}
        onValueChange={(value) => {
          onFiltersChange((current) => ({
            ...current,
            priority: value === "all" ? null : (value as TicketPriority),
            page: 1,
          }));
        }}
      >
        <SelectTrigger className="w-36" aria-label="Önceliğe göre filtrele">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(priorityOptions).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {/*
        "Mine" and "unassigned" are different questions, but they are the same
        decision for the person looking at the list, so they share one control.
      */}
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
        items={ticketSortLabels}
        value={filters.sort}
        onValueChange={(value) => {
          onFiltersChange((current) => ({ ...current, sort: value as TicketSort, page: 1 }));
        }}
      >
        <SelectTrigger className="w-44" aria-label="Sıralama">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(ticketSortLabels).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

function Surface({ children }: { children: React.ReactNode }) {
  return (
    <div className="border-border bg-card mt-5 overflow-hidden rounded-xl border">{children}</div>
  );
}

function TableSkeleton() {
  return (
    <div className="border-border bg-card mt-5 flex flex-col gap-3 rounded-xl border p-4">
      {[0, 1, 2, 3, 4].map((row) => (
        <div key={row} className="flex items-center gap-4">
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-16" />
          <Skeleton className="h-4 w-24" />
        </div>
      ))}
    </div>
  );
}
