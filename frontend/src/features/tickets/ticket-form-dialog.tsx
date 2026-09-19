"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldControl, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { ticketPriorityLabels } from "@/lib/domain-labels";
import { ApiError } from "@/lib/api/api-error";
import { TICKET_PRIORITIES, type TicketPriority } from "@/types/domain";
import { useCustomers } from "@/features/customers/customer-queries";
import { useMembers } from "@/features/team/team-queries";
import { useCreateTicket, useUpdateTicket } from "./ticket-queries";
import { ticketSchema, UNASSIGNED, type TicketFormValues } from "./ticket-schemas";
import { formatTicketNumber, type TicketDetail } from "./ticket-types";

/**
 * Raises a new ticket or edits an existing one.
 *
 * One dialog for both, because the fields are the same. The difference that
 * matters is invisible: an edit carries the row version it was opened with, so
 * a colleague saving in the meantime is reported instead of overwritten.
 */
export function TicketFormDialog({
  workspaceSlug,
  ticket,
  fixedCustomerId,
  open,
  onOpenChange,
}: {
  workspaceSlug: string;
  /** Present when editing; absent when raising a new ticket. */
  ticket?: TicketDetail;
  /** Preselects the customer when opened from their detail page. */
  fixedCustomerId?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        {/*
          Remounted on open so the fields pick up their initial values, and not
          rendered while closed so no draft is kept. A key rather than an effect
          that pushes values in after the first render.
        */}
        {open ? (
          <TicketForm
            key={ticket?.id ?? "new"}
            workspaceSlug={workspaceSlug}
            ticket={ticket}
            fixedCustomerId={fixedCustomerId}
            onDone={() => {
              onOpenChange(false);
            }}
          />
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function TicketForm({
  workspaceSlug,
  ticket,
  fixedCustomerId,
  onDone,
}: {
  workspaceSlug: string;
  ticket?: TicketDetail;
  fixedCustomerId?: string;
  onDone: () => void;
}) {
  const isEditing = ticket !== undefined;

  const createMutation = useCreateTicket(workspaceSlug);
  const updateMutation = useUpdateTicket(workspaceSlug, ticket?.id ?? "");
  const mutation = isEditing ? updateMutation : createMutation;

  /*
    The customer and assignee pickers need the full lists, so both are fetched
    at their first page with the largest page the API allows. A workspace with
    more customers than that needs a searchable picker, which is a real feature
    rather than a bigger number here.
  */
  const customersQuery = useCustomers(workspaceSlug, {
    search: "",
    status: null,
    includeArchived: false,
    sort: "NameAscending",
    page: 1,
  });

  const membersQuery = useMembers(workspaceSlug);

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    control,
    formState: { errors },
  } = useForm<TicketFormValues>({
    resolver: zodResolver(ticketSchema),
    defaultValues: toFormValues(ticket, fixedCustomerId),
  });

  const [formError, setFormError] = React.useState<string | null>(null);

  /*
    useWatch rather than the watch() the form returns. watch() is a function
    whose identity changes on every render, which the React compiler refuses to
    memoize around — it would skip optimising this whole component. useWatch is
    a hook and subscribes to one field, so only the parts that read it re-render.
  */
  const selectedCustomerId = useWatch({ control, name: "customerId" });
  const selectedPriority = useWatch({ control, name: "priority" });
  const selectedAssignee = useWatch({ control, name: "assignedUserId" });

  const customerOptions = React.useMemo(() => {
    const options: Record<string, string> = {};

    for (const customer of customersQuery.data?.items ?? []) {
      options[customer.id] = customer.name;
    }

    // An edited ticket may point at a customer the list does not carry — an
    // archived one, or one beyond the first page. Without this the picker would
    // fall back to showing a raw id.
    if (ticket !== undefined && !(ticket.customerId in options)) {
      options[ticket.customerId] = ticket.customerName;
    }

    return options;
  }, [customersQuery.data, ticket]);

  const assigneeOptions = React.useMemo(() => {
    const options: Record<string, string> = { [UNASSIGNED]: "Atanmamış" };

    for (const member of membersQuery.data ?? []) {
      options[member.userId] = member.displayName;
    }

    return options;
  }, [membersQuery.data]);

  const submit = handleSubmit((values) => {
    setFormError(null);

    const onError = (error: unknown) => {
      if (error instanceof ApiError && error.hasFieldErrors) {
        for (const [field, messages] of Object.entries(error.fieldErrors)) {
          if (isFormField(field) && messages.length > 0) {
            setError(field, { message: messages[0] });
          }
        }

        return;
      }

      setFormError(
        error instanceof ApiError ? error.message : "Kayıt edilemedi. Lütfen tekrar deneyin.",
      );
    };

    if (isEditing) {
      updateMutation.mutate(
        {
          subject: values.subject.trim(),
          description: values.description.trim(),
          priority: values.priority,
          customerId: values.customerId,
          version: ticket.version,
        },
        {
          onSuccess: (saved) => {
            toast.success("Talep güncellendi", { description: formatTicketNumber(saved.number) });
            onDone();
          },
          onError,
        },
      );

      return;
    }

    createMutation.mutate(
      {
        customerId: values.customerId,
        subject: values.subject.trim(),
        description: values.description.trim(),
        priority: values.priority,
        assignedUserId: values.assignedUserId === UNASSIGNED ? null : values.assignedUserId,
      },
      {
        onSuccess: (saved) => {
          toast.success("Talep açıldı", {
            description: `${formatTicketNumber(saved.number)} · ${saved.customerName}`,
          });
          onDone();
        },
        onError,
      },
    );
  });

  return (
    <form
      onSubmit={(event) => {
        void submit(event);
      }}
      noValidate
    >
      <DialogHeader>
        <DialogTitle>
          {isEditing ? `${formatTicketNumber(ticket.number)} düzenle` : "Yeni talep"}
        </DialogTitle>
        <DialogDescription>
          {isEditing
            ? "Talebin konusunu, açıklamasını ve önceliğini güncelleyin."
            : "Bir müşteri adına açılan destek talebini kaydedin."}
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <Field invalid={errors.subject !== undefined} className="sm:col-span-2">
          <FieldLabel required>Konu</FieldLabel>
          <FieldControl>
            <Input placeholder="Fatura PDF'i indirilemiyor" {...register("subject")} />
          </FieldControl>
          <FieldError>{errors.subject?.message}</FieldError>
        </Field>

        <Field invalid={errors.customerId !== undefined}>
          <FieldLabel required>Müşteri</FieldLabel>
          <FieldControl>
            <Select
              items={customerOptions}
              value={selectedCustomerId}
              onValueChange={(value) => {
                setValue("customerId", value as string, { shouldValidate: true });
              }}
              disabled={fixedCustomerId !== undefined}
            >
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Müşteri seçin" />
              </SelectTrigger>
              <SelectContent>
                {Object.entries(customerOptions).map(([id, name]) => (
                  <SelectItem key={id} value={id}>
                    {name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FieldControl>
          <FieldError>{errors.customerId?.message}</FieldError>
        </Field>

        <Field>
          <FieldLabel>Öncelik</FieldLabel>
          <FieldControl>
            <Select
              items={Object.fromEntries(
                TICKET_PRIORITIES.map((priority) => [
                  priority,
                  ticketPriorityLabels[priority].label,
                ]),
              )}
              value={selectedPriority}
              onValueChange={(value) => {
                setValue("priority", value as TicketPriority);
              }}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TICKET_PRIORITIES.map((priority) => (
                  <SelectItem key={priority} value={priority}>
                    {ticketPriorityLabels[priority].label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FieldControl>
        </Field>

        {/*
          Assignment is only offered when raising a ticket. On an existing one it
          lives on the detail page, where it is a single action with its own
          route rather than a field buried in a form.
        */}
        {isEditing ? null : (
          <Field className="sm:col-span-2">
            <FieldLabel>Atanan</FieldLabel>
            <FieldControl>
              <Select
                items={assigneeOptions}
                value={selectedAssignee}
                onValueChange={(value) => {
                  setValue("assignedUserId", value as string);
                }}
              >
                <SelectTrigger className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {Object.entries(assigneeOptions).map(([id, name]) => (
                    <SelectItem key={id} value={id}>
                      {name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldControl>
          </Field>
        )}

        <Field invalid={errors.description !== undefined} className="sm:col-span-2">
          <FieldLabel>Açıklama</FieldLabel>
          <FieldControl>
            <Textarea
              rows={5}
              placeholder="Sorunun nasıl ortaya çıktığını, hangi adımların denendiğini yazın."
              {...register("description")}
            />
          </FieldControl>
          <FieldError>{errors.description?.message}</FieldError>
        </Field>
      </div>

      {formError === null ? null : (
        <Alert variant="destructive" className="mt-4">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <DialogFooter className="mt-5">
        <Button type="button" variant="ghost" onClick={onDone}>
          Vazgeç
        </Button>
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Kaydediliyor…" : isEditing ? "Kaydet" : "Talebi aç"}
        </Button>
      </DialogFooter>
    </form>
  );
}

function toFormValues(ticket?: TicketDetail, fixedCustomerId?: string): TicketFormValues {
  if (ticket === undefined) {
    return {
      customerId: fixedCustomerId ?? "",
      subject: "",
      description: "",
      priority: "Medium",
      assignedUserId: UNASSIGNED,
    };
  }

  return {
    customerId: ticket.customerId,
    subject: ticket.subject,
    description: ticket.description,
    priority: ticket.priority,
    assignedUserId: ticket.assignedUserId ?? UNASSIGNED,
  };
}

function isFormField(field: string): field is keyof TicketFormValues {
  return ["customerId", "subject", "description", "priority", "assignedUserId"].includes(field);
}
