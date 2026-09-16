"use client";

import * as React from "react";
import Link from "next/link";
import { type ColumnDef, flexRender, getCoreRowModel, useReactTable } from "@tanstack/react-table";
import { Pagination } from "@/components/ui/pagination";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { TicketPriorityBadge, TicketStatusBadge } from "@/components/product/status-badge";
import { formatRelativeTime } from "@/lib/format";
import { formatTicketNumber, type TicketListItem } from "./ticket-types";

/**
 * The ticket table, shared by the ticket list and a customer's ticket tab.
 * @remarks
 * The customer column is dropped on the customer page, where every row would
 * carry the same name and the column would only take space from the subject.
 */
export function TicketTable({
  workspaceSlug,
  items,
  isStale,
  showCustomer = true,
  page,
  pageSize,
  totalCount,
  onPageChange,
}: {
  workspaceSlug: string;
  items: TicketListItem[];
  isStale: boolean;
  showCustomer?: boolean;
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}) {
  /*
    Opts this component out of the React compiler. TanStack Table returns
    functions whose identity changes on every call, which the compiler cannot
    memoize safely; this is the escape hatch TanStack documents for that case.
  */
  "use no memo";

  const columns = React.useMemo<ColumnDef<TicketListItem>[]>(() => {
    const defined: ColumnDef<TicketListItem>[] = [
      {
        accessorKey: "number",
        header: "Talep",
        cell: ({ row }) => (
          <Link
            href={`/app/${workspaceSlug}/tickets/${row.original.id}`}
            className="block min-w-0 rounded-sm"
          >
            {/*
              Monospace because the number is a technical identifier read
              character by character, which is the only place the design system
              uses it (docs/DESIGN_SYSTEM.md).
            */}
            <span className="text-muted-foreground block font-mono text-xs">
              {formatTicketNumber(row.original.number)}
            </span>
            <span className="text-foreground block truncate font-medium">
              {row.original.subject}
            </span>
          </Link>
        ),
      },
    ];

    if (showCustomer) {
      defined.push({
        accessorKey: "customerName",
        header: "Müşteri",
        cell: ({ row }) => (
          <Link
            href={`/app/${workspaceSlug}/customers/${row.original.customerId}`}
            className="text-muted-foreground hover:text-foreground block truncate rounded-sm transition-colors duration-100"
          >
            {row.original.customerName}
          </Link>
        ),
      });
    }

    defined.push(
      {
        accessorKey: "status",
        header: "Durum",
        cell: ({ row }) => <TicketStatusBadge status={row.original.status} />,
      },
      {
        accessorKey: "priority",
        header: "Öncelik",
        cell: ({ row }) => <TicketPriorityBadge priority={row.original.priority} />,
      },
      {
        accessorKey: "assignedUserDisplayName",
        header: "Atanan",
        cell: ({ row }) => (
          <span className="text-muted-foreground block truncate">
            {row.original.assignedUserDisplayName ?? "Atanmamış"}
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
    );

    return defined;
  }, [workspaceSlug, showCustomer]);

  // The "use no memo" directive above already tells the compiler to skip this
  // component; the rule then reports that it did so.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: items,
    columns,
    getCoreRowModel: getCoreRowModel(),
  });

  return (
    <div
      className="border-border bg-card mt-5 overflow-hidden rounded-xl border transition-opacity duration-100"
      // Dims while the next page loads rather than swapping in a skeleton,
      // which would make the layout jump on every keystroke.
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
