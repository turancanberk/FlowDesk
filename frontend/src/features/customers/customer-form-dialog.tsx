"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
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
import { customerStatusLabels } from "@/lib/domain-labels";
import { ApiError } from "@/lib/api/api-error";
import { useCreateCustomer, useUpdateCustomer } from "./customer-queries";
import { customerSchema, toCustomerInput, type CustomerFormValues } from "./customer-schemas";
import type { CustomerDetail, CustomerStatus } from "./customer-types";

const EMPTY_FORM: CustomerFormValues = {
  name: "",
  email: "",
  phone: "",
  company: "",
  status: "Active",
  notes: "",
};

/**
 * Creates or edits a customer.
 *
 * One dialog for both because the fields and the rules are identical; a second
 * component would be the same form with a different title and a second place
 * for a validation rule to drift.
 */
export function CustomerFormDialog({
  workspaceSlug,
  customer,
  open,
  onOpenChange,
}: {
  workspaceSlug: string;
  /** Present when editing; absent when creating. */
  customer?: CustomerDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        {/*
          The form is remounted whenever the dialog opens or the customer
          changes, which is how its fields get their initial values. React's own
          answer to "reset state when a prop changes" is a key, not an effect
          that pushes the new values in afterwards — the effect version renders
          once with stale values and then again with the right ones.

          Only rendered while open, so a closed dialog holds no draft.
        */}
        {open ? (
          <CustomerForm
            key={customer?.id ?? "new"}
            workspaceSlug={workspaceSlug}
            customer={customer}
            onDone={() => {
              onOpenChange(false);
            }}
          />
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

function CustomerForm({
  workspaceSlug,
  customer,
  onDone,
}: {
  workspaceSlug: string;
  customer?: CustomerDetail;
  onDone: () => void;
}) {
  const isEditing = customer !== undefined;

  const createMutation = useCreateCustomer(workspaceSlug);
  const updateMutation = useUpdateCustomer(workspaceSlug, customer?.id ?? "");
  const mutation = isEditing ? updateMutation : createMutation;

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<CustomerFormValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: toFormValues(customer),
  });

  const [formError, setFormError] = React.useState<string | null>(null);

  const submit = handleSubmit((values) => {
    setFormError(null);

    mutation.mutate(toCustomerInput(values), {
      onSuccess: (saved) => {
        toast.success(isEditing ? "Müşteri güncellendi" : "Müşteri eklendi", {
          description: saved.name,
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
        <DialogTitle>{isEditing ? "Müşteriyi düzenle" : "Yeni müşteri"}</DialogTitle>
        <DialogDescription>
          {isEditing
            ? "Müşteri bilgilerini güncelleyin."
            : "Hizmet verdiğiniz kurumu veya kişiyi kaydedin."}
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <Field invalid={errors.name !== undefined} className="sm:col-span-2">
          <FieldLabel required>Müşteri adı</FieldLabel>
          <FieldControl>
            <Input placeholder="Acme Teknoloji" {...register("name")} />
          </FieldControl>
          <FieldError>{errors.name?.message}</FieldError>
        </Field>

        <Field invalid={errors.company !== undefined}>
          <FieldLabel>Şirket</FieldLabel>
          <FieldControl>
            <Input placeholder="Acme Teknoloji A.Ş." {...register("company")} />
          </FieldControl>
          <FieldError>{errors.company?.message}</FieldError>
        </Field>

        <Field>
          <FieldLabel>Durum</FieldLabel>
          <FieldControl>
            <Select
              /*
                The labels as items, so the trigger shows "Aktif" rather than
                the domain code. Uncontrolled: a fixed value here pinned the
                trigger to the saved status, and choosing another one while
                editing changed the form but not what the person saw.
              */
              items={{
                Active: customerStatusLabels.Active.label,
                Inactive: customerStatusLabels.Inactive.label,
              }}
              defaultValue={customer?.status ?? "Active"}
              onValueChange={(value) => {
                setValue("status", value as CustomerStatus);
              }}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Active">{customerStatusLabels.Active.label}</SelectItem>
                <SelectItem value="Inactive">{customerStatusLabels.Inactive.label}</SelectItem>
              </SelectContent>
            </Select>
          </FieldControl>
        </Field>

        <Field invalid={errors.email !== undefined}>
          <FieldLabel>E-posta</FieldLabel>
          <FieldControl>
            <Input type="email" placeholder="iletisim@acme.test" {...register("email")} />
          </FieldControl>
          <FieldError>{errors.email?.message}</FieldError>
        </Field>

        <Field invalid={errors.phone !== undefined}>
          <FieldLabel>Telefon</FieldLabel>
          <FieldControl>
            <Input placeholder="+90 212 000 00 00" {...register("phone")} />
          </FieldControl>
          <FieldError>{errors.phone?.message}</FieldError>
        </Field>

        <Field invalid={errors.notes !== undefined} className="sm:col-span-2">
          <FieldLabel>Notlar</FieldLabel>
          <FieldControl>
            <Textarea rows={3} placeholder="Ekibin bilmesi gerekenler" {...register("notes")} />
          </FieldControl>
          <FieldError>{errors.notes?.message}</FieldError>
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

/** Builds the form's initial values from the record being edited. */
function toFormValues(customer?: CustomerDetail): CustomerFormValues {
  if (customer === undefined) {
    return EMPTY_FORM;
  }

  return {
    name: customer.name,
    email: customer.email ?? "",
    phone: customer.phone ?? "",
    company: customer.company ?? "",
    status: customer.status,
    notes: customer.notes ?? "",
  };
}

function isFormField(field: string): field is keyof CustomerFormValues {
  return ["name", "email", "phone", "company", "status", "notes"].includes(field);
}
