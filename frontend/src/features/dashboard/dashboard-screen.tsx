"use client";

import Link from "next/link";
import { AlertTriangleIcon, ArrowRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/product/empty-state";
import { PageHeader } from "@/components/product/page-header";
import {
  StatusBadge,
  TicketPriorityBadge,
  TicketStatusBadge,
} from "@/components/product/status-badge";
import { formatDate, formatNumber, formatRelativeTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { formatTicketNumber } from "@/features/tickets/ticket-types";
import { useDashboard } from "./dashboard-queries";
import { StatusDistribution } from "./status-distribution";
import type { DashboardSummary } from "./dashboard-types";

export function DashboardScreen({ workspace }: { workspace: Workspace }) {
  const { data, isPending, isError, error, refetch } = useDashboard(workspace.slug);

  if (isPending) {
    return <DashboardSkeleton />;
  }

  if (isError) {
    return (
      <div className="px-6 py-6">
        <PageHeader title="Dashboard" description={workspace.name} />
        <div className="border-border bg-card mt-6 overflow-hidden rounded-xl border">
          <EmptyState
            title="Özet yüklenemedi"
            description={
              error instanceof ApiError
                ? error.message
                : "Bağlantı kurulamadı. Tekrar denemek sorunu çözebilir."
            }
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
        </div>
      </div>
    );
  }

  return <DashboardContent workspace={workspace} summary={data} />;
}

function DashboardContent({
  workspace,
  summary,
}: {
  workspace: Workspace;
  summary: DashboardSummary;
}) {
  const base = `/app/${workspace.slug}`;

  return (
    <div className="px-6 py-6">
      <PageHeader
        title="Dashboard"
        // Says when the figures were taken rather than implying they are live.
        description={`${workspace.name} · ${formatRelativeTime(summary.generatedAt)} alındı`}
      />

      {/*
        A single strip divided by hairlines rather than six separate cards.
        Cards would push the numbers apart and turn a glance into a scan; these
        figures are read together, as one sentence about the workspace.
      */}
      <dl className="border-border bg-card mt-6 grid grid-cols-2 divide-x divide-y overflow-hidden rounded-xl border sm:grid-cols-3 lg:grid-cols-6 lg:divide-y-0">
        <Metric label="Müşteri" value={summary.customerCount} href={`${base}/customers`} />
        <Metric label="Açık talep" value={summary.openTicketCount} href={`${base}/tickets`} />
        {/*
          Links to the ticket list unfiltered, not to a pre-filtered view. The
          list keeps its filters in component state, so a query string here
          would land on an unfiltered page and quietly break the promise the
          link makes. Reading filters from the URL is worth doing, but it
          belongs to the list rather than to this number.
        */}
        <Metric
          label="Atanmamış"
          value={summary.unassignedTicketCount}
          href={`${base}/tickets`}
          // Unclaimed work is the one figure here that is a problem by its
          // nature, so it is marked when it is not zero.
          tone={summary.unassignedTicketCount > 0 ? "warning" : undefined}
        />
        <Metric
          label="Geciken görev"
          value={summary.overdueTaskCount}
          href={`${base}/tasks`}
          tone={summary.overdueTaskCount > 0 ? "danger" : undefined}
        />
        <Metric label="Bu hafta" value={summary.dueSoonTaskCount} href={`${base}/tasks`} />
        <Metric label="Ekip" value={summary.memberCount} href={`${base}/team`} />
      </dl>

      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <Panel title="Talep dağılımı" className="lg:col-span-1">
          <StatusDistribution counts={summary.ticketsByStatus} />
        </Panel>

        <Panel
          title="Yaklaşan görevler"
          className="lg:col-span-2"
          action={
            <Link
              href={`${base}/tasks`}
              className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 rounded-sm text-xs transition-colors duration-100"
            >
              Tümü
              <ArrowRightIcon className="size-3" />
            </Link>
          }
        >
          {summary.upcomingTasks.length === 0 ? (
            <p className="text-muted-foreground text-sm">Son tarihi olan bekleyen görev yok.</p>
          ) : (
            <ul className="divide-border -my-1 divide-y">
              {summary.upcomingTasks.map((task) => (
                <li key={task.id} className="flex items-center gap-3 py-2">
                  {task.isOverdue ? (
                    <AlertTriangleIcon
                      className="text-danger size-3.5 shrink-0"
                      aria-hidden="true"
                    />
                  ) : (
                    <span className="size-3.5 shrink-0" />
                  )}

                  <span className="text-foreground min-w-0 flex-1 truncate text-sm">
                    {task.title}
                  </span>

                  <span className="text-muted-foreground hidden w-28 shrink-0 truncate text-xs sm:block">
                    {task.assignedUserDisplayName ?? "Atanmamış"}
                  </span>

                  <span className="w-24 shrink-0 text-right">
                    {task.isOverdue ? (
                      <StatusBadge tone="danger">{formatDate(task.dueAt)}</StatusBadge>
                    ) : (
                      <span className="text-muted-foreground text-xs">
                        {formatDate(task.dueAt)}
                      </span>
                    )}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>

      <Panel
        title="Son hareket eden talepler"
        className="mt-4"
        action={
          <Link
            href={`${base}/tickets`}
            className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1 rounded-sm text-xs transition-colors duration-100"
          >
            Tümü
            <ArrowRightIcon className="size-3" />
          </Link>
        }
      >
        {summary.recentTickets.length === 0 ? (
          <p className="text-muted-foreground text-sm">Henüz talep yok.</p>
        ) : (
          <ul className="divide-border -my-1 divide-y">
            {summary.recentTickets.map((ticket) => (
              <li key={ticket.id} className="flex items-center gap-3 py-2">
                {/*
                  Monospace because the number is a technical identifier read
                  character by character (docs/DESIGN_SYSTEM.md).
                */}
                <Link
                  href={`${base}/tickets/${ticket.id}`}
                  className="text-muted-foreground hover:text-foreground w-20 shrink-0 rounded-sm font-mono text-xs transition-colors duration-100"
                >
                  {formatTicketNumber(ticket.number)}
                </Link>

                <Link
                  href={`${base}/tickets/${ticket.id}`}
                  className="text-foreground min-w-0 flex-1 truncate rounded-sm text-sm"
                >
                  {ticket.subject}
                </Link>

                <span className="text-muted-foreground hidden w-36 shrink-0 truncate text-xs md:block">
                  {ticket.customerName}
                </span>

                <span className="hidden shrink-0 sm:block">
                  <TicketPriorityBadge priority={ticket.priority} />
                </span>

                <span className="shrink-0">
                  <TicketStatusBadge status={ticket.status} />
                </span>

                <span className="text-muted-foreground w-20 shrink-0 text-right text-xs">
                  {formatRelativeTime(ticket.updatedAt)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  );
}

/**
 * One figure in the metric strip.
 *
 * A link, because a number on a dashboard is a question — and the answer is
 * always the list it came from.
 */
function Metric({
  label,
  value,
  href,
  tone,
}: {
  label: string;
  value: number;
  href: string;
  tone?: "warning" | "danger";
}) {
  return (
    <div className="border-border">
      <Link
        href={href}
        className="hover:bg-muted/50 flex flex-col gap-1 px-4 py-3 transition-colors duration-100"
      >
        <dt className="text-muted-foreground text-xs">{label}</dt>
        <dd
          className={
            tone === "danger"
              ? "text-danger text-2xl font-semibold tabular-nums"
              : tone === "warning"
                ? "text-warning text-2xl font-semibold tabular-nums"
                : "text-foreground text-2xl font-semibold tabular-nums"
          }
        >
          {formatNumber(value)}
        </dd>
      </Link>
    </div>
  );
}

function Panel({
  title,
  action,
  className,
  children,
}: {
  title: string;
  action?: React.ReactNode;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <section className={`border-border bg-card rounded-xl border ${className ?? ""}`}>
      <div className="border-border flex items-center justify-between border-b px-4 py-2.5">
        <h2 className="text-foreground text-sm font-semibold">{title}</h2>
        {action}
      </div>
      <div className="p-4">{children}</div>
    </section>
  );
}

function DashboardSkeleton() {
  return (
    <div className="px-6 py-6">
      <Skeleton className="h-7 w-40" />
      <Skeleton className="mt-2 h-4 w-56" />
      <Skeleton className="mt-6 h-20 rounded-xl" />
      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <Skeleton className="h-56 rounded-xl" />
        <Skeleton className="h-56 rounded-xl lg:col-span-2" />
      </div>
      <Skeleton className="mt-4 h-48 rounded-xl" />
    </div>
  );
}
