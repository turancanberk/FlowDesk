"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Field, FieldControl, FieldError } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { EmptyState } from "@/components/product/empty-state";
import { formatDateTime, formatRelativeTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import { useAddTicketComment, useTicketComments } from "./ticket-queries";
import { ticketCommentSchema, type TicketCommentFormValues } from "./ticket-schemas";

/**
 * The ticket's conversation, oldest first.
 *
 * Read top to bottom like a thread rather than paged: splitting a support
 * conversation across pages breaks the reading for no gain at the lengths these
 * reach.
 */
export function TicketComments({
  workspaceSlug,
  ticketId,
  canComment,
}: {
  workspaceSlug: string;
  ticketId: string;
  canComment: boolean;
}) {
  const { data, isPending, isError, refetch } = useTicketComments(workspaceSlug, ticketId);

  return (
    <div className="flex flex-col gap-4">
      {isPending ? (
        <div className="flex flex-col gap-3">
          <Skeleton className="h-16" />
          <Skeleton className="h-16" />
        </div>
      ) : isError ? (
        <EmptyState
          title="Yorumlar yüklenemedi"
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
      ) : data.length === 0 ? (
        <p className="text-muted-foreground py-6 text-center text-sm">
          Bu talebe henüz yorum yazılmamış.
        </p>
      ) : (
        <ol className="flex flex-col gap-4">
          {data.map((comment) => (
            <li key={comment.id} className="flex gap-3">
              <Avatar className="mt-0.5 size-7 shrink-0">
                <AvatarFallback className="text-[10px]">
                  {initials(comment.authorDisplayName)}
                </AvatarFallback>
              </Avatar>

              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline gap-x-2">
                  <span className="text-foreground text-sm font-medium">
                    {comment.authorDisplayName}
                  </span>
                  {/*
                    Relative time is what a reader scans; the exact moment is a
                    tooltip away for when it matters.
                  */}
                  <time
                    dateTime={comment.createdAt}
                    title={formatDateTime(comment.createdAt)}
                    className="text-muted-foreground text-xs"
                  >
                    {formatRelativeTime(comment.createdAt)}
                  </time>
                </div>
                <p className="text-foreground mt-1 text-sm whitespace-pre-wrap">{comment.body}</p>
              </div>
            </li>
          ))}
        </ol>
      )}

      {canComment ? (
        <CommentComposer workspaceSlug={workspaceSlug} ticketId={ticketId} />
      ) : (
        <p className="text-muted-foreground border-border border-t pt-4 text-xs">
          İzleyici rolü talepleri görüntüler, yorum yazamaz.
        </p>
      )}
    </div>
  );
}

function CommentComposer({ workspaceSlug, ticketId }: { workspaceSlug: string; ticketId: string }) {
  const mutation = useAddTicketComment(workspaceSlug, ticketId);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TicketCommentFormValues>({
    resolver: zodResolver(ticketCommentSchema),
    defaultValues: { body: "" },
  });

  const submit = handleSubmit((values) => {
    mutation.mutate(values.body, {
      onSuccess: () => {
        reset({ body: "" });
      },
      onError: (error) => {
        toast.error("Yorum eklenemedi", {
          description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
        });
      },
    });
  });

  return (
    <form
      onSubmit={(event) => {
        void submit(event);
      }}
      noValidate
      className="border-border border-t pt-4"
    >
      <Field invalid={errors.body !== undefined}>
        <FieldControl>
          <Textarea
            rows={3}
            placeholder="Ne yapıldığını ekibin görebileceği şekilde yazın."
            aria-label="Yorum"
            {...register("body")}
          />
        </FieldControl>
        <FieldError>{errors.body?.message}</FieldError>
      </Field>

      <div className="mt-2 flex justify-end">
        <Button type="submit" size="sm" disabled={mutation.isPending}>
          {mutation.isPending ? "Ekleniyor…" : "Yorum ekle"}
        </Button>
      </div>
    </form>
  );
}

function initials(displayName: string): string {
  return displayName
    .split(/\s+/)
    .filter((part) => part.length > 0)
    .slice(0, 2)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR") ?? "")
    .join("");
}
