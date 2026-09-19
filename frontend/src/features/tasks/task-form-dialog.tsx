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
import { ApiError } from "@/lib/api/api-error";
import { useCustomers } from "@/features/customers/customer-queries";
import { useMembers } from "@/features/team/team-queries";
import { useCreateTask, useUpdateTask } from "./task-queries";
import { NONE, taskSchema, type TaskFormValues } from "./task-schemas";
import type { TaskDetail, TaskInput } from "./task-types";

/**
 * Creates or edits a task.
 *
 * One dialog for both, because the fields and the rules are identical and the
 * update replaces every one of them anyway.
 */
export function TaskFormDialog({
  workspaceSlug,
  task,
  fixedCustomerId,
  open,
  onOpenChange,
}: {
  workspaceSlug: string;
  /** Present when editing; absent when creating. */
  task?: TaskDetail;
  /** Preselects the customer when opened from their detail page. */
  fixedCustomerId?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        {/*
          Remounted on open so the fields pick up their initial values, and not
          rendered while closed so no draft is kept.
        */}
        {open ? (
          <TaskForm
            key={task?.id ?? "new"}
            workspaceSlug={workspaceSlug}
            task={task}
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

function TaskForm({
  workspaceSlug,
  task,
  fixedCustomerId,
  onDone,
}: {
  workspaceSlug: string;
  task?: TaskDetail;
  fixedCustomerId?: string;
  onDone: () => void;
}) {
  const isEditing = task !== undefined;

  const createMutation = useCreateTask(workspaceSlug);
  const updateMutation = useUpdateTask(workspaceSlug, task?.id ?? "");
  const mutation = isEditing ? updateMutation : createMutation;

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
  } = useForm<TaskFormValues>({
    resolver: zodResolver(taskSchema),
    defaultValues: toFormValues(task, fixedCustomerId),
  });

  const [formError, setFormError] = React.useState<string | null>(null);

  /*
    useWatch rather than the watch() the form returns: watch() is a function
    whose identity changes every render, which makes the React compiler skip
    optimising this component entirely.
  */
  const selectedCustomer = useWatch({ control, name: "customerId" });
  const selectedAssignee = useWatch({ control, name: "assignedUserId" });

  const customerOptions = React.useMemo(() => {
    const options: Record<string, string> = { [NONE]: "Müşteri yok" };

    for (const customer of customersQuery.data?.items ?? []) {
      options[customer.id] = customer.name;
    }

    // An edited task may point at a customer the list does not carry — an
    // archived one, or one beyond the first page.
    if (task?.customerId != null && !(task.customerId in options)) {
      options[task.customerId] = task.customerName ?? "Bilinmeyen müşteri";
    }

    return options;
  }, [customersQuery.data, task]);

  const assigneeOptions = React.useMemo(() => {
    const options: Record<string, string> = { [NONE]: "Atanmamış" };

    for (const member of membersQuery.data ?? []) {
      options[member.userId] = member.displayName;
    }

    if (task?.assignedUserId != null && !(task.assignedUserId in options)) {
      options[task.assignedUserId] = task.assignedUserDisplayName ?? "Bilinmeyen kullanıcı";
    }

    return options;
  }, [membersQuery.data, task]);

  const submit = handleSubmit((values) => {
    setFormError(null);

    mutation.mutate(toTaskInput(values), {
      onSuccess: (saved) => {
        toast.success(isEditing ? "Görev güncellendi" : "Görev eklendi", {
          description: saved.title,
        });
        onDone();
      },
      onError: (error) => {
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
      },
    });
  });

  return (
    <form
      onSubmit={(event) => {
        void submit(event);
      }}
      noValidate
    >
      <DialogHeader>
        <DialogTitle>{isEditing ? "Görevi düzenle" : "Yeni görev"}</DialogTitle>
        <DialogDescription>
          {isEditing
            ? "Görevin ayrıntılarını güncelleyin."
            : "Ekibin takip etmesi gereken işi kaydedin."}
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <Field invalid={errors.title !== undefined} className="sm:col-span-2">
          <FieldLabel required>Başlık</FieldLabel>
          <FieldControl>
            <Input placeholder="Sözleşmeyi gözden geçir" {...register("title")} />
          </FieldControl>
          <FieldError>{errors.title?.message}</FieldError>
        </Field>

        <Field invalid={errors.dueAt !== undefined}>
          {/*
            Optional on purpose. Forcing a date makes people invent one, and an
            invented deadline makes the overdue list untrustworthy.
          */}
          <FieldLabel>Son tarih</FieldLabel>
          <FieldControl>
            <Input type="date" {...register("dueAt")} />
          </FieldControl>
          <FieldError>{errors.dueAt?.message}</FieldError>
        </Field>

        <Field>
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

        <Field className="sm:col-span-2">
          <FieldLabel>Müşteri</FieldLabel>
          <FieldControl>
            <Select
              items={customerOptions}
              value={selectedCustomer}
              onValueChange={(value) => {
                setValue("customerId", value as string);
              }}
              disabled={fixedCustomerId !== undefined}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
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
        </Field>

        <Field invalid={errors.description !== undefined} className="sm:col-span-2">
          <FieldLabel>Açıklama</FieldLabel>
          <FieldControl>
            <Textarea
              rows={4}
              placeholder="Ekibin bilmesi gerekenler"
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
          {mutation.isPending ? "Kaydediliyor…" : "Kaydet"}
        </Button>
      </DialogFooter>
    </form>
  );
}

function toFormValues(task?: TaskDetail, fixedCustomerId?: string): TaskFormValues {
  if (task === undefined) {
    return {
      title: "",
      description: "",
      dueAt: "",
      customerId: fixedCustomerId ?? NONE,
      assignedUserId: NONE,
    };
  }

  return {
    title: task.title,
    description: task.description ?? "",
    dueAt: toDateInputValue(task.dueAt),
    customerId: task.customerId ?? NONE,
    assignedUserId: task.assignedUserId ?? NONE,
  };
}

/**
 * Turns the form's values into what the API expects.
 *
 * Every field is sent, including the empty ones as null: the update replaces
 * rather than patches, so clearing a field is how it is cleared.
 */
function toTaskInput(values: TaskFormValues): TaskInput {
  return {
    title: values.title.trim(),
    description: values.description.trim().length === 0 ? null : values.description.trim(),
    customerId: values.customerId === NONE ? null : values.customerId,
    assignedUserId: values.assignedUserId === NONE ? null : values.assignedUserId,
    dueAt: fromDateInputValue(values.dueAt),
  };
}

/** `2026-03-14`, which is what a native date input reads and writes. */
function toDateInputValue(value: string | null): string {
  if (value === null) {
    return "";
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 10);
}

/**
 * Turns a date-only input into an instant.
 *
 * Pinned to the end of the chosen day in the viewer's own timezone, because
 * "due on the 14th" means the task is late once the 14th is over where the
 * person works — not at midnight UTC, which for Türkiye would make it late
 * three hours early.
 */
function fromDateInputValue(value: string): string | null {
  if (value.trim().length === 0) {
    return null;
  }

  const [year, month, day] = value.split("-").map(Number);

  if (year === undefined || month === undefined || day === undefined) {
    return null;
  }

  return new Date(year, month - 1, day, 23, 59, 59).toISOString();
}

function isFormField(field: string): field is keyof TaskFormValues {
  return ["title", "description", "dueAt", "customerId", "assignedUserId"].includes(field);
}
