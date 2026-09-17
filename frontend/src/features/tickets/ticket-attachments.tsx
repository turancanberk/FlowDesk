"use client";

import * as React from "react";
import { DownloadIcon, PaperclipIcon, Trash2Icon, UploadIcon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime, formatFileSize, formatRelativeTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import { downloadAttachment } from "./ticket-api";
import {
  useDeleteAttachment,
  useTicketAttachments,
  useUploadAttachment,
} from "./ticket-queries";
import type { TicketAttachment } from "./ticket-types";

/**
 * The files attached to a ticket.
 *
 * Downloads go through the API with the bearer token rather than a plain link:
 * the container is private and a link without a token would be refused — which
 * is the point, because a link that worked without one would make the file
 * reachable by anyone who had the address.
 */
export function TicketAttachments({
  workspaceSlug,
  ticketId,
  canManage,
}: {
  workspaceSlug: string;
  ticketId: string;
  canManage: boolean;
}) {
  const { data, isPending, isError, refetch } = useTicketAttachments(workspaceSlug, ticketId);
  const upload = useUploadAttachment(workspaceSlug, ticketId);

  const inputRef = React.useRef<HTMLInputElement>(null);

  return (
    <div className="flex flex-col gap-3">
      {isPending ? (
        <div className="flex flex-col gap-2">
          <Skeleton className="h-9" />
          <Skeleton className="h-9" />
        </div>
      ) : isError ? (
        <div className="py-2">
          <p className="text-muted-foreground text-sm">Dosyalar yüklenemedi.</p>
          <Button
            size="sm"
            variant="outline"
            className="mt-2"
            onClick={() => {
              void refetch();
            }}
          >
            Tekrar dene
          </Button>
        </div>
      ) : data.length === 0 ? (
        <p className="text-muted-foreground text-sm">Bu talebe dosya eklenmemiş.</p>
      ) : (
        <ul className="divide-border divide-y">
          {data.map((attachment) => (
            <li key={attachment.id}>
              <AttachmentRow
                workspaceSlug={workspaceSlug}
                ticketId={ticketId}
                attachment={attachment}
                canManage={canManage}
              />
            </li>
          ))}
        </ul>
      )}

      {canManage ? (
        <div className="border-border border-t pt-3">
          <input
            ref={inputRef}
            type="file"
            className="sr-only"
            /*
              Accepts the same list the server enforces. It is a convenience in
              the file picker, not a check: the browser's filter can be turned
              off in the dialog, so the rule that matters is the one on the
              server (docs/SECURITY.md).
            */
            accept="image/png,image/jpeg,image/gif,image/webp,application/pdf,text/plain,text/csv,application/zip,.doc,.docx,.xls,.xlsx"
            onChange={(event) => {
              const file = event.target.files?.[0];

              // Cleared straight away so choosing the same file twice in a row
              // still fires a change event.
              event.target.value = "";

              if (file === undefined) {
                return;
              }

              upload.mutate(file, {
                onSuccess: (saved) => {
                  toast.success("Dosya eklendi", { description: saved.fileName });
                },
                onError: (error) => {
                  toast.error("Dosya eklenemedi", {
                    description:
                      error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                  });
                },
              });
            }}
          />

          <Button
            size="sm"
            variant="outline"
            disabled={upload.isPending}
            onClick={() => {
              inputRef.current?.click();
            }}
          >
            <UploadIcon />
            {upload.isPending ? "Yükleniyor…" : "Dosya ekle"}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

function AttachmentRow({
  workspaceSlug,
  ticketId,
  attachment,
  canManage,
}: {
  workspaceSlug: string;
  ticketId: string;
  attachment: TicketAttachment;
  canManage: boolean;
}) {
  const [isDownloading, setIsDownloading] = React.useState(false);
  const remove = useDeleteAttachment(workspaceSlug, ticketId);

  return (
    <div className="flex items-center gap-3 py-2">
      <PaperclipIcon className="text-muted-foreground size-3.5 shrink-0" aria-hidden="true" />

      <span className="min-w-0 flex-1">
        <span className="text-foreground block truncate text-sm">{attachment.fileName}</span>
        <span className="text-muted-foreground block text-xs">
          {formatFileSize(attachment.sizeInBytes)}
          {" · "}
          <time dateTime={attachment.createdAt} title={formatDateTime(attachment.createdAt)}>
            {formatRelativeTime(attachment.createdAt)}
          </time>
        </span>
      </span>

      <Button
        variant="ghost"
        size="icon-sm"
        aria-label={`${attachment.fileName} dosyasını indir`}
        disabled={isDownloading}
        onClick={() => {
          setIsDownloading(true);

          void (async () => {
            try {
              const { blob, fileName } = await downloadAttachment(
                workspaceSlug,
                ticketId,
                attachment.id,
              );

              /*
                The bytes arrive through fetch, so the browser has to be handed
                them: an object URL is created, a link clicked, and the URL
                revoked. Leaving it alive would hold the whole file in memory
                for as long as the page is open.
              */
              const url = URL.createObjectURL(blob);
              const link = document.createElement("a");

              link.href = url;
              link.download = fileName ?? attachment.fileName;
              document.body.append(link);
              link.click();
              link.remove();
              URL.revokeObjectURL(url);
            } catch (error) {
              toast.error("Dosya indirilemedi", {
                description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
              });
            } finally {
              setIsDownloading(false);
            }
          })();
        }}
      >
        <DownloadIcon />
      </Button>

      {canManage ? (
        <Button
          variant="ghost"
          size="icon-sm"
          aria-label={`${attachment.fileName} dosyasını sil`}
          disabled={remove.isPending}
          onClick={() => {
            remove.mutate(attachment.id, {
              onSuccess: () => {
                toast.success("Dosya silindi", { description: attachment.fileName });
              },
              onError: (error) => {
                toast.error("Dosya silinemedi", {
                  description:
                    error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                });
              },
            });
          }}
        >
          <Trash2Icon />
        </Button>
      ) : null}
    </div>
  );
}
