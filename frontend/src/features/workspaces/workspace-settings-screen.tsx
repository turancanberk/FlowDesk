"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { PageHeader } from "@/components/product/page-header";
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
import {
  Field,
  FieldControl,
  FieldDescription,
  FieldError,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api/api-error";
import { membershipRoleLabels } from "@/lib/domain-labels";
import { useDeleteWorkspace, useUpdateWorkspace } from "./workspace-queries";
import { updateWorkspaceSchema, type UpdateWorkspaceFormValues } from "./workspace-schemas";
import type { Workspace } from "./workspace-types";

/**
 * The workspace's own settings: its name, and deleting it.
 *
 * The API for both has existed since Phase 04; the sidebar linked here from
 * the start and led to a 404 until Phase 17's browser tests followed the link.
 * Who may do what mirrors the permission matrix — renaming is an admin's,
 * deleting an owner's (docs/SECURITY.md) — and the API enforces it regardless.
 */
export function WorkspaceSettingsScreen({ workspace }: { workspace: Workspace }) {
  const canRename = workspace.role === "Owner" || workspace.role === "Admin";
  const canDelete = workspace.role === "Owner";

  return (
    <div className="px-6 py-6">
      <PageHeader title="Ayarlar" description="Çalışma alanının adı ve kalıcı işlemler." />

      <div className="mt-6 flex max-w-2xl flex-col gap-6">
        <Surface>
          <div className="p-5">
            <h2 className="text-foreground mb-4 text-sm font-semibold">Genel</h2>
            {canRename ? (
              <RenameForm workspace={workspace} />
            ) : (
              <ReadOnlyGeneral workspace={workspace} />
            )}
          </div>
        </Surface>

        {canDelete ? (
          <Surface className="border-danger-border">
            <div className="p-5">
              <h2 className="text-foreground text-sm font-semibold">Çalışma alanını sil</h2>
              <p className="text-muted-foreground mt-1 mb-4 text-sm">
                Müşteriler, talepler, görevler, dosyalar ve ekip üyelikleri kalıcı olarak silinir.
                Bu işlem geri alınamaz.
              </p>
              <DeleteWorkspace workspace={workspace} />
            </div>
          </Surface>
        ) : null}
      </div>
    </div>
  );
}

function RenameForm({ workspace }: { workspace: Workspace }) {
  const updateMutation = useUpdateWorkspace(workspace.slug);

  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty },
  } = useForm<UpdateWorkspaceFormValues>({
    resolver: zodResolver(updateWorkspaceSchema),
    defaultValues: { name: workspace.name },
  });

  const [formError, setFormError] = React.useState<string | null>(null);

  const submit = handleSubmit((values) => {
    setFormError(null);

    updateMutation.mutate(values, {
      onSuccess: (updated) => {
        // The saved name becomes the new baseline, so the button goes quiet.
        reset({ name: updated.name });
        toast.success("Çalışma alanı adı güncellendi", { description: updated.name });
      },
      onError: (error) => {
        // The client checks the same rule; a server-side refusal still lands
        // on the field it is about.
        const nameError = error instanceof ApiError ? error.fieldErrors["name"]?.[0] : undefined;

        if (nameError !== undefined) {
          setError("name", { message: nameError });
          return;
        }

        setFormError(
          error instanceof ApiError ? error.message : "Ad kaydedilemedi. Lütfen tekrar deneyin.",
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
          <Input autoComplete="organization" {...register("name")} />
        </FieldControl>
        {/* The address does not follow the name: links people already have keep working. */}
        <FieldDescription>Adres değişmez: /app/{workspace.slug}</FieldDescription>
        <FieldError>{errors.name?.message}</FieldError>
      </Field>

      <div>
        <Button type="submit" size="sm" disabled={!isDirty || updateMutation.isPending}>
          {updateMutation.isPending ? "Kaydediliyor…" : "Kaydet"}
        </Button>
      </div>
    </form>
  );
}

function ReadOnlyGeneral({ workspace }: { workspace: Workspace }) {
  return (
    <dl className="flex flex-col gap-3 text-sm">
      <div>
        <dt className="text-muted-foreground text-xs">Çalışma alanı adı</dt>
        <dd className="text-foreground mt-0.5">{workspace.name}</dd>
      </div>
      <div>
        <dt className="text-muted-foreground text-xs">Adres</dt>
        <dd className="text-foreground mt-0.5 font-mono text-xs">/app/{workspace.slug}</dd>
      </div>
      <p className="text-muted-foreground">
        Rolünüz {membershipRoleLabels[workspace.role].label}. Ayarları yöneticiler değiştirebilir.
      </p>
    </dl>
  );
}

function DeleteWorkspace({ workspace }: { workspace: Workspace }) {
  const router = useRouter();
  const deleteMutation = useDeleteWorkspace(workspace.slug);

  const [isConfirming, setIsConfirming] = React.useState(false);
  const [typedName, setTypedName] = React.useState("");

  // Typing the name, not just clicking twice: everything in the workspace goes,
  // for everyone in it, and a second click is too easy to give by reflex.
  const confirmed = typedName.trim() === workspace.name;

  return (
    <>
      <Button
        size="sm"
        variant="destructive"
        onClick={() => {
          setTypedName("");
          setIsConfirming(true);
        }}
      >
        Çalışma alanını sil
      </Button>

      <Dialog open={isConfirming} onOpenChange={setIsConfirming}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Çalışma alanını sil</DialogTitle>
            <DialogDescription>
              {workspace.name} ve içindeki her şey kalıcı olarak silinecek. Bu işlem geri alınamaz.
            </DialogDescription>
          </DialogHeader>

          <Field className="mt-4">
            <FieldLabel>Onaylamak için çalışma alanının adını yazın</FieldLabel>
            <FieldControl>
              <Input
                value={typedName}
                autoComplete="off"
                onChange={(event) => {
                  setTypedName(event.target.value);
                }}
              />
            </FieldControl>
          </Field>

          <DialogFooter className="mt-5">
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setIsConfirming(false);
              }}
            >
              Vazgeç
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={!confirmed || deleteMutation.isPending}
              onClick={() => {
                deleteMutation.mutate(undefined, {
                  onSuccess: () => {
                    toast.success("Çalışma alanı silindi", { description: workspace.name });
                    router.replace("/");
                  },
                  onError: (error) => {
                    toast.error("Çalışma alanı silinemedi", {
                      description:
                        error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                    });
                  },
                });
              }}
            >
              Kalıcı olarak sil
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

function Surface({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <section
      className={`border-border bg-card overflow-hidden rounded-xl border ${className ?? ""}`}
    >
      {children}
    </section>
  );
}
