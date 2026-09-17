"use client";

import * as React from "react";
import Link from "next/link";
import {
  ActivityIcon,
  BuildingIcon,
  ListChecksIcon,
  PaperclipIcon,
  TicketIcon,
  UsersIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Pagination } from "@/components/ui/pagination";
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
import { activitySubjectLabels, activityTypeLabels } from "@/lib/domain-labels";
import { formatDateTime, formatRelativeTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { useActivity } from "./activity-queries";
import {
  ACTIVITY_SUBJECTS,
  INITIAL_ACTIVITY_FILTERS,
  type ActivityFilters,
  type ActivityItem,
  type ActivitySubject,
} from "./activity-types";

export function ActivityScreen({ workspace }: { workspace: Workspace }) {
  const [filters, setFilters] = React.useState<ActivityFilters>(INITIAL_ACTIVITY_FILTERS);

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useActivity(
    workspace.slug,
    filters,
  );

  return (
    <div className="px-6 py-6">
      <PageHeader
        title="Etkinlik"
        description="Çalışma alanında yapılan önemli işlemlerin kaydı."
      />

      <div className="mt-5 flex flex-wrap items-center gap-3">
        <Select
          items={{
            all: "Tüm kayıtlar",
            ...Object.fromEntries(
              ACTIVITY_SUBJECTS.map((subject) => [subject, activitySubjectLabels[subject]]),
            ),
          }}
          value={filters.subjectType ?? "all"}
          onValueChange={(value) => {
            setFilters((current) => ({
              ...current,
              subjectType: value === "all" ? null : (value as ActivitySubject),
              page: 1,
            }));
          }}
        >
          <SelectTrigger className="w-44" aria-label="Kayıt türüne göre filtrele">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Tüm kayıtlar</SelectItem>
            {ACTIVITY_SUBJECTS.map((subject) => (
              <SelectItem key={subject} value={subject}>
                {activitySubjectLabels[subject]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {filters.subjectType !== null ? (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => {
              setFilters(INITIAL_ACTIVITY_FILTERS);
            }}
          >
            Filtreyi temizle
          </Button>
        ) : null}
      </div>

      <div className="mt-5">
        {isPending ? (
          <Surface>
            <div className="flex flex-col gap-3 p-4">
              {[0, 1, 2, 3, 4].map((row) => (
                <Skeleton key={row} className="h-10" />
              ))}
            </div>
          </Surface>
        ) : isError ? (
          <Surface>
            <EmptyState
              title="Etkinlik geçmişi yüklenemedi"
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
          </Surface>
        ) : data.items.length === 0 ? (
          <Surface>
            <EmptyState
              icon={<ActivityIcon />}
              title={
                filters.subjectType === null
                  ? "Henüz kayıtlı etkinlik yok"
                  : "Bu türde etkinlik yok"
              }
              description="Müşteri, talep ve görev üzerinde yapılan işlemler burada görünecek."
            />
          </Surface>
        ) : (
          <Surface>
            <ol
              className="divide-border divide-y transition-opacity duration-100"
              // Dims while the next page loads rather than emptying the list.
              style={{ opacity: isPlaceholderData ? 0.6 : 1 }}
            >
              {data.items.map((item) => (
                <li key={item.id}>
                  <ActivityRow workspaceSlug={workspace.slug} item={item} />
                </li>
              ))}
            </ol>

            <Pagination
              page={data.page}
              pageSize={data.pageSize}
              totalCount={data.totalCount}
              onPageChange={(page) => {
                setFilters((current) => ({ ...current, page }));
              }}
            />
          </Surface>
        )}
      </div>
    </div>
  );
}

function ActivityRow({
  workspaceSlug,
  item,
}: {
  workspaceSlug: string;
  item: ActivityItem;
}) {
  const subject = describeSubject(item);
  const href = linkFor(workspaceSlug, item);

  const line = (
    <>
      <span className="text-foreground font-medium">
        {/*
          "Sistem" when nobody did it, "Bilinmeyen kullanıcı" when the account
          is gone. Both are honest; inventing a name would not be.
        */}
        {item.actorDisplayName ?? (item.actorUserId === null ? "Sistem" : "Bilinmeyen kullanıcı")}
      </span>{" "}
      {activityTypeLabels[item.type]}
      {subject === null ? null : (
        <>
          {": "}
          <span className="text-foreground">{subject}</span>
        </>
      )}
    </>
  );

  return (
    <div className="hover:bg-muted/50 flex items-start gap-3 px-4 py-2.5 transition-colors duration-100">
      <SubjectIcon subjectType={item.subjectType} />

      <div className="min-w-0 flex-1">
        <p className="text-muted-foreground text-sm">
          {href === null ? line : <Link href={href} className="rounded-sm">{line}</Link>}
        </p>
        <time
          dateTime={item.occurredAt}
          title={formatDateTime(item.occurredAt)}
          className="text-muted-foreground mt-0.5 block text-xs"
        >
          {formatRelativeTime(item.occurredAt)}
        </time>
      </div>
    </div>
  );
}

function SubjectIcon({ subjectType }: { subjectType: ActivityItem["subjectType"] }) {
  const className = "text-muted-foreground mt-0.5 size-3.5 shrink-0";

  switch (subjectType) {
    case "Customer":
      return <BuildingIcon className={className} aria-hidden="true" />;
    case "Ticket":
      return <TicketIcon className={className} aria-hidden="true" />;
    case "TaskItem":
      return <ListChecksIcon className={className} aria-hidden="true" />;
    case "Member":
      return <UsersIcon className={className} aria-hidden="true" />;
    case "Attachment":
      return <PaperclipIcon className={className} aria-hidden="true" />;
    default:
      return <ActivityIcon className={className} aria-hidden="true" />;
  }
}

/**
 * The part of the line that names what was acted on.
 *
 * Returns null when the payload does not carry one, which happens when a client
 * is older than the server that wrote it. The line still reads without it.
 */
function describeSubject(item: ActivityItem): string | null {
  const payload = item.payload as Record<string, unknown> | null;

  if (payload === null || typeof payload !== "object") {
    return null;
  }

  if (typeof payload.number === "number" && typeof payload.subject === "string") {
    return `TLP-${payload.number} ${payload.subject}`;
  }

  if (typeof payload.fileName === "string") {
    return payload.fileName;
  }

  if (typeof payload.name === "string") {
    return payload.name;
  }

  if (typeof payload.title === "string") {
    return payload.title;
  }

  if (typeof payload.email === "string") {
    return payload.email;
  }

  return null;
}

/**
 * Where the line points, when it points anywhere.
 *
 * Deleted subjects get no link: the record survives its subject on purpose, and
 * a link to something that is gone is worse than none.
 */
function linkFor(workspaceSlug: string, item: ActivityItem): string | null {
  const base = `/app/${workspaceSlug}`;

  switch (item.type) {
    case "TicketDeleted":
    case "TaskDeleted":
    case "MemberRemoved":
      return null;

    case "AttachmentUploaded":
    case "AttachmentDeleted": {
      const payload = item.payload as { ticketId?: unknown };

      return typeof payload?.ticketId === "string"
        ? `${base}/tickets/${payload.ticketId}`
        : null;
    }

    default:
      break;
  }

  switch (item.subjectType) {
    case "Customer":
      return `${base}/customers/${item.subjectId}`;
    case "Ticket":
      return `${base}/tickets/${item.subjectId}`;
    case "TaskItem":
      return `${base}/tasks`;
    case "Member":
      return `${base}/team`;
    default:
      return null;
  }
}

function Surface({ children }: { children: React.ReactNode }) {
  return <div className="border-border bg-card overflow-hidden rounded-xl border">{children}</div>;
}
