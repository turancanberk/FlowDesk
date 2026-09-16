"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm, useWatch } from "react-hook-form";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Field,
  FieldControl,
  FieldDescription,
  FieldError,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/api-error";
import { useCreateWorkspace } from "./workspace-queries";
import { createWorkspaceSchema, type CreateWorkspaceFormValues } from "./workspace-schemas";
import { suggestSlug } from "./suggest-slug";

export function CreateWorkspaceForm() {
  const router = useRouter();
  const createMutation = useCreateWorkspace();

  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors },
  } = useForm<CreateWorkspaceFormValues>({
    resolver: zodResolver(createWorkspaceSchema),
    defaultValues: { name: "", slug: "" },
  });

  const [formError, setFormError] = React.useState<string | null>(null);

  /*
    The address preview is derived during render from the two fields, not
    mirrored into state by an effect. An effect would add a render pass and a
    second source of truth for something the fields already determine.

    useWatch subscribes to just these two fields rather than re-rendering on
    every keystroke anywhere in the form, and unlike the form-level watch() it
    is safe for the React compiler to memoize.
  */
  const name = useWatch({ control, name: "name" });
  const slug = useWatch({ control, name: "slug" });
  const previewSlug = slug.trim().length > 0 ? slug.trim() : suggestSlug(name);

  const submit = handleSubmit((values) => {
    setFormError(null);

    createMutation.mutate(
      { name: values.name, slug: values.slug.trim() === "" ? undefined : values.slug.trim() },
      {
        onSuccess: (workspace) => {
          router.replace(`/app/${workspace.slug}/dashboard`);
        },
        onError: (error) => {
          if (error instanceof ApiError && error.code === "workspace.slug_taken") {
            setError("slug", { message: error.message });
            return;
          }

          setFormError(
            error instanceof ApiError
              ? error.message
              : "Çalışma alanı oluşturulamadı. Lütfen tekrar deneyin.",
          );
        },
      },
    );
  });

  return (
    <form
      onSubmit={(event) => {
        void submit(event);
      }}
      noValidate
      className="flex flex-col gap-4"
    >
      {formError === null ? null : (
        <Alert variant="destructive">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      )}

      <Field invalid={errors.name !== undefined} described>
        <FieldLabel required>Çalışma alanı adı</FieldLabel>
        <FieldControl>
          <Input placeholder="Acme Teknoloji" autoComplete="organization" {...register("name")} />
        </FieldControl>
        <FieldDescription>Ekibinizin göreceği ad.</FieldDescription>
        <FieldError>{errors.name?.message}</FieldError>
      </Field>

      <Field invalid={errors.slug !== undefined} described>
        <FieldLabel>Adres</FieldLabel>
        <FieldControl>
          <Input
            placeholder={suggestSlug(name) || "acme-teknoloji"}
            className="font-mono text-xs"
            {...register("slug")}
          />
        </FieldControl>
        <FieldDescription>
          Boş bırakırsanız addan türetilir.{" "}
          {previewSlug.length > 0 ? (
            <>
              Adres: <span className="font-mono">/app/{previewSlug}</span>
            </>
          ) : null}
        </FieldDescription>
        <FieldError>{errors.slug?.message}</FieldError>
      </Field>

      <Button type="submit" size="lg" disabled={createMutation.isPending}>
        {createMutation.isPending ? "Oluşturuluyor…" : "Çalışma alanı oluştur"}
      </Button>
    </form>
  );
}
