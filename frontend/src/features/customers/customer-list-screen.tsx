"use client";

import * as React from "react";
import Link from "next/link";
import { type ColumnDef, flexRender, getCoreRowModel, useReactTable } from "@tanstack/react-table";
import {
  ArchiveIcon,
  BuildingIcon,
  MoreHorizontalIcon,
  PlusIcon,
  RotateCcwIcon,
  SearchIcon,
  SearchXIcon,
} from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Pagination } from "@/components/ui/pagination";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { EmptyState } from "@/components/product/empty-state";
import { PageHeader } from "@/components/product/page-header";
import { CustomerStatusBadge, StatusBadge } from "@/components/product/status-badge";
import { customerSortLabels, customerStatusLabels } from "@/lib/domain-labels";
import { formatRelativeTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { CustomerFormDialog } from "./customer-form-dialog";
import { useArchiveCustomer, useCustomers, useRestoreCustomer } from "./customer-queries";
import type {
  CustomerFilters,
  CustomerListItem,
  CustomerSort,
  CustomerStatus,
} from "./customer-types";

const INITIAL_FILTERS: CustomerFilters = {
  search: "",
  status: null,
  includeArchived: false,
  sort: "RecentlyUpdated",
  page: 1,
};

export function CustomerListScreen({ workspace }: { workspace: Workspace }) {
  const [filters, setFilters] = React.useState<CustomerFilters>(INITIAL_FILTERS);
  const [searchInput, setSearchInput] = React.useState("");
  const [isCreating, setIsCreating] = React.useState(false);

  const canManage = workspace.role !== "Viewer";
  const canArchive = workspace.role === "Admin" || workspace.role === "Owner";

  /*
    The search box is debounced so that typing does not fire a request per
    keystroke. The input keeps its own state and the committed value lands in
    the filters, which is what the query key is built from.
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

  const { data, isPending, isError, isPlaceholderData, refetch } = useCustomers(
    workspace.slug,
    filters,
  );

  const hasActiveFilters =
    filters.search.trim().length > 0 || filters.status !== null || filters.includeArchived;

  return (
    <div className="px-6 py-6">
      <PageHeader
        title="Müşteriler"
        description="Hizmet verdiğiniz kurumlar ve kişiler."
        actions={
          canManage ? (
            <Button
              size="sm"
              onClick={() => {
                setIsCreating(true);
              }}
            >
              <PlusIcon />
              Yeni müşteri
            </Button>
          ) : null
        }
      />

      <CustomerFilterBar
        filters={filters}
        searchInput={searchInput}
        onSearchChange={setSearchInput}
        onFiltersChange={setFilters}
      />

      {isPending ? (
        <TableSkeleton />
      ) : isError ? (
        <Surface>
          <EmptyState
            title="Müşteriler yüklenemedi"
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
            /*
              "No records at all" and "no records matching this filter" are
              different situations and get different screens: one invites the
              user to create something, the other to widen their search.
            */
            <EmptyState
              icon={<SearchXIcon />}
              title="Filtreye uyan müşteri yok"
              description="Arama terimini veya seçtiğiniz filtreleri değiştirmeyi deneyin."
              action={
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setSearchInput("");
                    setFilters(INITIAL_FILTERS);
                  }}
                >
                  Filtreleri temizle
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={<BuildingIcon />}
              title="Henüz müşteri yok"
              description="İlk müşterinizi ekledikten sonra ona talep ve görev açabilirsiniz."
              action={
                canManage ? (
                  <Button
                    size="sm"
                    onClick={() => {
                      setIsCreating(true);
                    }}
                  >
                    <PlusIcon />
                    Yeni müşteri
                  </Button>
                ) : null
              }
            />
          )}
        </Surface>
      ) : (
        <CustomerTable
          workspace={workspace}
          items={data.items}
          canManage={canManage}
          canArchive={canArchive}
          isStale={isPlaceholderData}
          page={data.page}
          pageSize={data.pageSize}
          totalCount={data.totalCount}
          onPageChange={(page) => {
            setFilters((current) => ({ ...current, page }));
          }}
        />
      )}

      <CustomerFormDialog
        workspaceSlug={workspace.slug}
        open={isCreating}
        onOpenChange={setIsCreating}
      />
    </div>
  );
}

function CustomerFilterBar({
  filters,
  searchInput,
  onSearchChange,
  onFiltersChange,
}: {
  filters: CustomerFilters;
  searchInput: string;
  onSearchChange: (value: string) => void;
  onFiltersChange: React.Dispatch<React.SetStateAction<CustomerFilters>>;
}) {
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
          placeholder="Ad, şirket veya e-posta ara"
          aria-label="Müşteri ara"
          className="pl-8"
        />
      </div>

      {/*
        Base UI resolves the trigger label from `items`; without it the trigger
        shows the raw value, which would put English codes like "Active" into a
        Turkish interface.
      */}
      <Select
        items={{
          all: "Tüm durumlar",
          Active: customerStatusLabels.Active.label,
          Inactive: customerStatusLabels.Inactive.label,
        }}
        value={filters.status ?? "all"}
        onValueChange={(value) => {
          onFiltersChange((current) => ({
            ...current,
            status: value === "all" ? null : (value as CustomerStatus),
            page: 1,
          }));
        }}
      >
        <SelectTrigger className="w-36" aria-label="Duruma göre filtrele">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">Tüm durumlar</SelectItem>
          <SelectItem value="Active">{customerStatusLabels.Active.label}</SelectItem>
          <SelectItem value="Inactive">{customerStatusLabels.Inactive.label}</SelectItem>
        </SelectContent>
      </Select>

      <Select
        items={customerSortLabels}
        value={filters.sort}
        onValueChange={(value) => {
          onFiltersChange((current) => ({ ...current, sort: value as CustomerSort, page: 1 }));
        }}
      >
        <SelectTrigger className="w-44" aria-label="Sıralama">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {Object.entries(customerSortLabels).map(([value, label]) => (
            <SelectItem key={value} value={value}>
              {label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <div className="flex items-center gap-2">
        <Checkbox
          id="include-archived"
          checked={filters.includeArchived}
          onCheckedChange={(checked) => {
            onFiltersChange((current) => ({
              ...current,
              includeArchived: checked === true,
              page: 1,
            }));
          }}
        />
        <Label htmlFor="include-archived" className="font-normal">
          Arşivlenenleri göster
        </Label>
      </div>
    </div>
  );
}

function CustomerTable({
  workspace,
  items,
  canManage,
  canArchive,
  isStale,
  page,
  pageSize,
  totalCount,
  onPageChange,
}: {
  workspace: Workspace;
  items: CustomerListItem[];
  canManage: boolean;
  canArchive: boolean;
  isStale: boolean;
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  /*
    Opts this component out of the React compiler. TanStack Table returns
    functions whose identity changes on every call, which the compiler cannot
    memoize safely; this is the escape hatch TanStack documents for exactly
    that case. Scoped to this component, so the rest of the file keeps
    compiler optimisation.
  */
  "use no memo";

  const columns = React.useMemo<ColumnDef<CustomerListItem>[]>(
    () => [
      {
        accessorKey: "name",
        header: "Müşteri",
        cell: ({ row }) => (
          <Link
            href={`/app/${workspace.slug}/customers/${row.original.id}`}
            className="block min-w-0 rounded-sm"
          >
            <span className="text-foreground block truncate font-medium">{row.original.name}</span>
            {row.original.company === null ? null : (
              <span className="text-muted-foreground block truncate text-xs">
                {row.original.company}
              </span>
            )}
          </Link>
        ),
      },
      {
        accessorKey: "email",
        header: "İletişim",
        cell: ({ row }) => (
          <span className="text-muted-foreground block truncate">
            {row.original.email ?? row.original.phone ?? "—"}
          </span>
        ),
      },
      {
        accessorKey: "status",
        header: "Durum",
        cell: ({ row }) => (
          <span className="flex items-center gap-1.5">
            <CustomerStatusBadge status={row.original.status} />
            {row.original.isArchived ? <StatusBadge tone="neutral">Arşivli</StatusBadge> : null}
          </span>
        ),
      },
      {
        accessorKey: "updatedAt",
        header: () => <span className="block text-right">Güncellendi</span>,
        cell: ({ row }) => (
          <span className="text-muted-foreground block text-right">
            {formatRelativeTime(row.original.updatedAt)}
          </span>
        ),
      },
    ],
    [workspace.slug],
  );

  // The "use no memo" directive above already tells the compiler to skip this
  // component; the rule then reports that it did so. Acknowledged rather than
  // left as recurring noise in every lint run.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div
      className="border-border bg-card mt-5 overflow-hidden rounded-xl border transition-opacity duration-100"
      // Dims the table while the next page loads rather than replacing it with
      // a skeleton, which would make the layout jump on every keystroke.
      style={{ opacity: isStale ? 0.6 : 1 }}
    >
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id} className="hover:bg-muted">
              {headerGroup.headers.map((header) => (
                <TableHead key={header.id}>
                  {header.isPlaceholder
                    ? null
                    : flexRender(header.column.columnDef.header, header.getContext())}
                </TableHead>
              ))}
              {canManage ? <TableHead className="w-12" /> : null}
            </TableRow>
          ))}
        </TableHeader>

        <TableBody>
          {table.getRowModel().rows.map((row) => (
            <TableRow key={row.id}>
              {row.getVisibleCells().map((cell) => (
                <TableCell key={cell.id} className="max-w-xs">
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </TableCell>
              ))}
              {canManage ? (
                <TableCell className="text-right">
                  <CustomerRowActions
                    workspaceSlug={workspace.slug}
                    customer={row.original}
                    canArchive={canArchive}
                  />
                </TableCell>
              ) : null}
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={onPageChange}
      />
    </div>
  );
}

function CustomerRowActions({
  workspaceSlug,
  customer,
  canArchive,
}: {
  workspaceSlug: string;
  customer: CustomerListItem;
  canArchive: boolean;
}) {
  const archiveMutation = useArchiveCustomer(workspaceSlug);
  const restoreMutation = useRestoreCustomer(workspaceSlug);

  function reportFailure(error: unknown, fallback: string) {
    toast.error(fallback, {
      description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
    });
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon-sm" aria-label={`${customer.name} için işlemler`}>
            <MoreHorizontalIcon />
          </Button>
        }
      />

      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuItem
          nativeButton={false}
          render={(props) => (
            <Link {...props} href={`/app/${workspaceSlug}/customers/${customer.id}`} />
          )}
        >
          Ayrıntıları aç
        </DropdownMenuItem>

        {customer.isArchived ? (
          <DropdownMenuItem
            disabled={!canArchive || restoreMutation.isPending}
            onClick={() => {
              restoreMutation.mutate(customer.id, {
                onSuccess: () => {
                  toast.success("Müşteri arşivden çıkarıldı", { description: customer.name });
                },
                onError: (error) => {
                  reportFailure(error, "Arşivden çıkarılamadı");
                },
              });
            }}
          >
            <RotateCcwIcon />
            Arşivden çıkar
          </DropdownMenuItem>
        ) : (
          <DropdownMenuItem
            variant="destructive"
            disabled={!canArchive || archiveMutation.isPending}
            onClick={() => {
              archiveMutation.mutate(customer.id, {
                onSuccess: () => {
                  toast.success("Müşteri arşivlendi", {
                    description: `${customer.name} listeden kaldırıldı, geçmişi korundu.`,
                  });
                },
                onError: (error) => {
                  reportFailure(error, "Müşteri arşivlenemedi");
                },
              });
            }}
          >
            <ArchiveIcon />
            Arşivle
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
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
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-24" />
        </div>
      ))}
    </div>
  );
}
