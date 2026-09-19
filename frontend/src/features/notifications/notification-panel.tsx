"use client";

import * as React from "react";
import Link from "next/link";
import { BellIcon, CheckCheckIcon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Skeleton } from "@/components/ui/skeleton";
import { formatDateTime, formatNumber, formatRelativeTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import { useMarkNotificationsRead, useNotifications } from "./notification-queries";
import type {
  NotificationItem,
  TicketAssignedPayload,
  TicketCommentedPayload,
} from "./notification-types";

/**
 * The notification bell and its panel.
 *
 * Lives in the sidebar rather than a top bar, because there is no top bar: the
 * product's pages own their whole width and adding one would push every screen
 * down for a control most people use a few times a day.
 */
export function NotificationPanel({ workspaceSlug }: { workspaceSlug: string }) {
  const [isOpen, setIsOpen] = React.useState(false);

  const { data, isPending, isError, refetch } = useNotifications(workspaceSlug);
  const markRead = useMarkNotificationsRead(workspaceSlug);

  const unreadCount = data?.unreadCount ?? 0;

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger
        render={
          <button
            type="button"
            className="hover:bg-sidebar-accent flex h-8 w-full items-center gap-2 rounded-md px-2 text-left text-sm transition-colors duration-100"
            // The count is in the label, not only in the badge: a screen reader
            // reads the button, not the coloured circle beside it.
            aria-label={
              unreadCount > 0
                ? `Bildirimler, ${formatNumber(unreadCount)} okunmamış`
                : "Bildirimler"
            }
          >
            <BellIcon className="text-muted-foreground size-3.5 shrink-0" />
            <span className="text-foreground flex-1 truncate">Bildirimler</span>
            {unreadCount > 0 ? (
              <span
                aria-hidden="true"
                className="bg-primary text-primary-foreground flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-medium tabular-nums"
              >
                {formatNumber(unreadCount)}
              </span>
            ) : null}
          </button>
        }
      />

      {/* Named, because it opens as a dialog and an unnamed one is announced as just "dialog". */}
      <PopoverContent side="right" align="end" className="w-80 p-0" aria-label="Bildirimler">
        <div className="border-border flex items-center justify-between border-b px-3 py-2">
          <span className="text-foreground text-sm font-semibold">Bildirimler</span>

          {unreadCount > 0 ? (
            <Button
              variant="ghost"
              size="sm"
              disabled={markRead.isPending}
              onClick={() => {
                // An empty list means everything, which is what this button is.
                markRead.mutate([], {
                  onError: (error) => {
                    toast.error("Bildirimler okundu işaretlenemedi", {
                      description:
                        error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                    });
                  },
                });
              }}
            >
              <CheckCheckIcon />
              Tümünü okundu say
            </Button>
          ) : null}
        </div>

        <div className="max-h-96 overflow-y-auto">
          {isPending ? (
            <div className="flex flex-col gap-2 p-3">
              <Skeleton className="h-10" />
              <Skeleton className="h-10" />
            </div>
          ) : isError ? (
            <div className="p-4 text-center">
              <p className="text-muted-foreground text-sm">Bildirimler yüklenemedi.</p>
              <Button
                size="sm"
                variant="outline"
                className="mt-3"
                onClick={() => {
                  void refetch();
                }}
              >
                Tekrar dene
              </Button>
            </div>
          ) : data.items.length === 0 ? (
            <p className="text-muted-foreground p-6 text-center text-sm">Henüz bildiriminiz yok.</p>
          ) : (
            <ol className="divide-border divide-y">
              {data.items.map((item) => (
                <li key={item.id}>
                  <NotificationRow
                    item={item}
                    onOpen={() => {
                      setIsOpen(false);

                      if (!item.isRead) {
                        markRead.mutate([item.id]);
                      }
                    }}
                  />
                </li>
              ))}
            </ol>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

function NotificationRow({ item, onOpen }: { item: NotificationItem; onOpen: () => void }) {
  const notice = describe(item);

  if (notice === null) {
    /*
      An unrecognised type or a payload that does not match it. Skipped rather
      than rendered as a blank row: a notice nobody can read is noise, and a
      client running against a newer server will meet one.
    */
    return null;
  }

  return (
    <Link
      href={notice.href}
      onClick={onOpen}
      className="hover:bg-muted/50 flex gap-2.5 px-3 py-2.5 transition-colors duration-100"
    >
      {/* Unread is marked with a dot and with weight, not with colour alone. */}
      <span
        aria-hidden="true"
        className={
          item.isRead
            ? "mt-1.5 size-1.5 shrink-0 rounded-full bg-transparent"
            : "bg-primary mt-1.5 size-1.5 shrink-0 rounded-full"
        }
      />

      <span className="min-w-0 flex-1">
        <span
          className={
            item.isRead
              ? "text-muted-foreground block text-sm"
              : "text-foreground block text-sm font-medium"
          }
        >
          {notice.title}
        </span>
        <span className="text-muted-foreground block truncate text-xs">{notice.detail}</span>
        <time
          dateTime={item.createdAt}
          title={formatDateTime(item.createdAt)}
          className="text-muted-foreground mt-0.5 block text-xs"
        >
          {formatRelativeTime(item.createdAt)}
        </time>
      </span>
    </Link>
  );
}

type Notice = { title: string; detail: string; href: string };

/**
 * Turns a stored payload into a line of Turkish and a link.
 *
 * Returns null when the payload does not match its type, which happens when a
 * client is older than the server that wrote it. Guessing would put a
 * half-rendered row in front of someone.
 */
function describe(item: NotificationItem): Notice | null {
  switch (item.type) {
    case "TicketAssigned": {
      const payload = item.payload as Partial<TicketAssignedPayload>;

      if (
        typeof payload?.ticketId !== "string" ||
        typeof payload.ticketNumber !== "number" ||
        typeof payload.workspaceSlug !== "string"
      ) {
        return null;
      }

      return {
        title: `TLP-${payload.ticketNumber} size atandı`,
        detail: payload.subject ?? "",
        href: `/app/${payload.workspaceSlug}/tickets/${payload.ticketId}`,
      };
    }

    case "TicketCommented": {
      const payload = item.payload as Partial<TicketCommentedPayload>;

      if (
        typeof payload?.ticketId !== "string" ||
        typeof payload.ticketNumber !== "number" ||
        typeof payload.workspaceSlug !== "string"
      ) {
        return null;
      }

      return {
        title: `${payload.authorDisplayName ?? "Bir ekip üyesi"}, TLP-${payload.ticketNumber} talebine yorum yazdı`,
        detail: payload.excerpt ?? "",
        href: `/app/${payload.workspaceSlug}/tickets/${payload.ticketId}`,
      };
    }

    default:
      return null;
  }
}
