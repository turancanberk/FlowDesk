"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeftIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
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
import { TicketPriorityBadge, TicketStatusBadge } from "@/components/product/status-badge";
import { ticketStatusLabels } from "@/lib/domain-labels";
import { formatDateTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import type { TicketStatus } from "@/types/domain";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { useMembers } from "@/features/team/team-queries";
import { TicketAttachments } from "./ticket-attachments";
import { TicketComments } from "./ticket-comments";
import { TicketFormDialog } from "./ticket-form-dialog";
import {
  useAssignTicket,
  useChangeTicketStatus,
  useDeleteTicket,
  useTicket,
} from "./ticket-queries";
import { UNASSIGNED } from "./ticket-schemas";
import { formatTicketNumber, type TicketDetail } from "./ticket-types";

export function TicketDetailScreen({
  workspace,
  ticketId,
}: {
  workspace: Workspace;
  ticketId: string;
}) {
  const { data: ticket, isPending, isError, error } = useTicket(workspace.slug, ticketId);

  if (isPending) {
    return <DetailSkeleton />;
  }

  if (isError || ticket === undefined) {
    return (
      <div className="px-6 py-6">
        <EmptyState
          title="Talep bulunamadı"
          description={
            error instanceof ApiError
              ? error.message
              : "Bu kayda erişilemiyor ya da kaldırılmış olabilir."
          }
          action={
            <Button
              size="sm"
              nativeButton={false}
              render={(props) => <Link {...props} href={`/app/${workspace.slug}/tickets`} />}
            >
              Talep listesi
            </Button>
          }
        />
      </div>
    );
  }

  return <TicketDetailContent workspace={workspace} ticket={ticket} />;
}

function TicketDetailContent({
  workspace,
  ticket,
}: {
  workspace: Workspace;
  ticket: TicketDetail;
}) {
  const router = useRouter();
  const [isEditing, setIsEditing] = React.useState(false);
  const [isConfirmingDelete, setIsConfirmingDelete] = React.useState(false);

  const canManage = workspace.role !== "Viewer";
  const canDelete = workspace.role === "Admin" || workspace.role === "Owner";

  const deleteMutation = useDeleteTicket(workspace.slug);

  return (
    <div className="px-6 py-6">
      <Link
        href={`/app/${workspace.slug}/tickets`}
        className="text-muted-foreground hover:text-foreground mb-4 inline-flex items-center gap-1.5 rounded-sm text-sm transition-colors duration-100"
      >
        <ArrowLeftIcon className="size-3.5" />
        Talepler
      </Link>

      <PageHeader
        // The number is a technical identifier, so it is set in mono and kept
        // next to the subject rather than replacing it.
        title={ticket.subject}
        description={`${formatTicketNumber(ticket.number)} · ${ticket.customerName}`}
        actions={
          canManage ? (
            <>
              {canDelete ? (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setIsConfirmingDelete(true);
                  }}
                >
                  <Trash2Icon />
                  Sil
                </Button>
              ) : null}

              <Button
                size="sm"
                onClick={() => {
                  setIsEditing(true);
                }}
              >
                <PencilIcon />
                Düzenle
              </Button>
            </>
          ) : null
        }
      />

      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <div className="flex flex-col gap-4 lg:col-span-2">
          <Surface>
            <div className="p-4">
              <h2 className="text-foreground text-sm font-semibold">Açıklama</h2>
              {ticket.description.length === 0 ? (
                <p className="text-muted-foreground mt-2 text-sm">
                  Bu talep için açıklama girilmemiş.
                </p>
              ) : (
                <p className="text-foreground mt-2 text-sm whitespace-pre-wrap">
                  {ticket.description}
                </p>
              )}
            </div>
          </Surface>

          <Surface>
            <div className="p-4">
              <h2 className="text-foreground mb-4 text-sm font-semibold">Dosyalar</h2>
              <TicketAttachments
                workspaceSlug={workspace.slug}
                ticketId={ticket.id}
                canManage={canManage}
              />
            </div>
          </Surface>

          <Surface>
            <div className="p-4">
              <h2 className="text-foreground mb-4 text-sm font-semibold">Yorumlar</h2>
              <TicketComments
                workspaceSlug={workspace.slug}
                ticketId={ticket.id}
                canComment={canManage}
              />
            </div>
          </Surface>
        </div>

        <Surface>
          <dl className="divide-border divide-y">
            <DetailRow label="Durum">
              {canManage ? (
                <StatusControl workspace={workspace} ticket={ticket} />
              ) : (
                <TicketStatusBadge status={ticket.status} />
              )}
            </DetailRow>

            <DetailRow label="Öncelik">
              <TicketPriorityBadge priority={ticket.priority} />
            </DetailRow>

            <DetailRow label="Atanan">
              {canManage ? (
                <AssigneeControl workspace={workspace} ticket={ticket} />
              ) : (
                (ticket.assignedUserDisplayName ?? "Atanmamış")
              )}
            </DetailRow>

            <DetailRow label="Müşteri">
              <Link
                href={`/app/${workspace.slug}/customers/${ticket.customerId}`}
                className="text-primary rounded-sm hover:underline"
              >
                {ticket.customerName}
              </Link>
            </DetailRow>

            <DetailRow label="Açan">{ticket.createdByDisplayName ?? "—"}</DetailRow>
            <DetailRow label="Açıldı">{formatDateTime(ticket.createdAt)}</DetailRow>
            <DetailRow label="Güncellendi">{formatDateTime(ticket.updatedAt)}</DetailRow>
            <DetailRow label="Çözüldü">
              {ticket.resolvedAt === null ? "—" : formatDateTime(ticket.resolvedAt)}
            </DetailRow>
          </dl>
        </Surface>
      </div>

      <TicketFormDialog
        workspaceSlug={workspace.slug}
        ticket={ticket}
        open={isEditing}
        onOpenChange={setIsEditing}
      />

      <Dialog open={isConfirmingDelete} onOpenChange={setIsConfirmingDelete}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Talebi sil</DialogTitle>
            <DialogDescription>
              {formatTicketNumber(ticket.number)} numaralı talep ve tüm yorumları kalıcı olarak
              silinecek. Bu işlem geri alınamaz.
            </DialogDescription>
          </DialogHeader>

          <DialogFooter className="mt-5">
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setIsConfirmingDelete(false);
              }}
            >
              Vazgeç
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={deleteMutation.isPending}
              onClick={() => {
                deleteMutation.mutate(ticket.id, {
                  onSuccess: () => {
                    toast.success("Talep silindi", {
                      description: formatTicketNumber(ticket.number),
                    });
                    router.push(`/app/${workspace.slug}/tickets`);
                  },
                  onError: (error) => {
                    toast.error("Talep silinemedi", {
                      description:
                        error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                    });
                  },
                });
              }}
            >
              {deleteMutation.isPending ? "Siliniyor…" : "Sil"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/**
 * Moves the ticket between statuses.
 *
 * Offers only the transitions the server sent with the ticket, so the control
 * never shows a move the domain would refuse.
 */
function StatusControl({ workspace, ticket }: { workspace: Workspace; ticket: TicketDetail }) {
  const mutation = useChangeTicketStatus(workspace.slug, ticket.id);

  const options: Record<string, string> = {
    [ticket.status]: ticketStatusLabels[ticket.status].label,
    ...Object.fromEntries(
      ticket.availableTransitions.map((status) => [status, ticketStatusLabels[status].label]),
    ),
  };

  return (
    <Select
      items={options}
      value={ticket.status}
      disabled={mutation.isPending}
      onValueChange={(value) => {
        const next = value as TicketStatus;

        if (next === ticket.status) {
          return;
        }

        mutation.mutate(next, {
          onSuccess: (saved) => {
            toast.success("Durum güncellendi", {
              description: ticketStatusLabels[saved.status].label,
            });
          },
          onError: (error) => {
            toast.error("Durum değiştirilemedi", {
              description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
            });
          },
        });
      }}
    >
      <SelectTrigger className="w-full" aria-label="Durum">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {Object.entries(options).map(([value, label]) => (
          <SelectItem key={value} value={value}>
            {label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function AssigneeControl({ workspace, ticket }: { workspace: Workspace; ticket: TicketDetail }) {
  const membersQuery = useMembers(workspace.slug);
  const mutation = useAssignTicket(workspace.slug, ticket.id);

  const options: Record<string, string> = { [UNASSIGNED]: "Atanmamış" };

  for (const member of membersQuery.data ?? []) {
    options[member.userId] = member.displayName;
  }

  // Someone who has since left the workspace may still be named on the ticket;
  // without this the control would show a blank rather than who holds it.
  if (ticket.assignedUserId !== null && !(ticket.assignedUserId in options)) {
    options[ticket.assignedUserId] = ticket.assignedUserDisplayName ?? "Bilinmeyen kullanıcı";
  }

  return (
    <Select
      items={options}
      value={ticket.assignedUserId ?? UNASSIGNED}
      disabled={mutation.isPending}
      onValueChange={(value) => {
        const next = value === UNASSIGNED ? null : (value as string);

        if (next === ticket.assignedUserId) {
          return;
        }

        mutation.mutate(next, {
          onSuccess: (saved) => {
            toast.success(saved.assignedUserId === null ? "Atama kaldırıldı" : "Talep atandı", {
              description: saved.assignedUserDisplayName ?? undefined,
            });
          },
          onError: (error) => {
            toast.error("Atama değiştirilemedi", {
              description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
            });
          },
        });
      }}
    >
      <SelectTrigger className="w-full" aria-label="Atanan">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {Object.entries(options).map(([value, label]) => (
          <SelectItem key={value} value={value}>
            {label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function DetailRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[6rem_1fr] items-center gap-3 px-4 py-2.5">
      <dt className="text-muted-foreground text-xs">{label}</dt>
      <dd className="text-foreground text-sm">{children}</dd>
    </div>
  );
}

function Surface({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`border-border bg-card overflow-hidden rounded-xl border ${className ?? ""}`}>
      {children}
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="px-6 py-6">
      <Skeleton className="h-4 w-24" />
      <Skeleton className="mt-4 h-7 w-80" />
      <Skeleton className="mt-2 h-4 w-52" />
      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <div className="flex flex-col gap-4 lg:col-span-2">
          <Skeleton className="h-32" />
          <Skeleton className="h-48" />
        </div>
        <Skeleton className="h-72" />
      </div>
    </div>
  );
}
