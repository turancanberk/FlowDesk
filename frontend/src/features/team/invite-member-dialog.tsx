"use client";

import * as React from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { CopyIcon, PlusIcon } from "lucide-react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Field,
  FieldControl,
  FieldDescription,
  FieldError,
  FieldLabel,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { membershipRoleLabels } from "@/lib/domain-labels";
import { ApiError } from "@/lib/api/api-error";
import type { MembershipRole } from "@/features/workspaces/workspace-types";
import { useInviteMember } from "./team-queries";
import type { CreatedInvitation } from "./team-types";

const inviteSchema = z.object({
  email: z.string().min(1, "E-posta adresi zorunludur.").email("Geçerli bir e-posta adresi girin."),
  role: z.enum(["Viewer", "Agent", "Admin", "Owner"]),
});

type InviteFormValues = z.infer<typeof inviteSchema>;

/** Roles an Owner may hand out; an Admin is offered everything except Owner. */
const assignableRoles: MembershipRole[] = ["Viewer", "Agent", "Admin", "Owner"];

export function InviteMemberDialog({
  workspaceSlug,
  callerRole,
}: {
  workspaceSlug: string;
  callerRole: MembershipRole;
}) {
  const [open, setOpen] = React.useState(false);
  const [created, setCreated] = React.useState<CreatedInvitation | null>(null);

  const inviteMutation = useInviteMember(workspaceSlug);

  const {
    register,
    handleSubmit,
    setError,
    setValue,
    reset,
    formState: { errors },
  } = useForm<InviteFormValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { email: "", role: "Agent" },
  });

  const offeredRoles =
    callerRole === "Owner" ? assignableRoles : assignableRoles.filter((role) => role !== "Owner");

  const submit = handleSubmit((values) => {
    inviteMutation.mutate(values, {
      onSuccess: (invitation) => {
        setCreated(invitation);
        toast.success("Davet oluşturuldu", {
          description: `${values.email} adresi için bağlantı hazır.`,
        });
      },
      onError: (error) => {
        if (error instanceof ApiError) {
          setError("email", { message: error.message });
          return;
        }

        setError("email", { message: "Davet oluşturulamadı. Lütfen tekrar deneyin." });
      },
    });
  });

  function close() {
    setOpen(false);
    setCreated(null);
    reset();
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(nextOpen) => {
        if (nextOpen) {
          setOpen(true);
        } else {
          close();
        }
      }}
    >
      <DialogTrigger
        render={
          <Button size="sm">
            <PlusIcon />
            Ekip üyesi davet et
          </Button>
        }
      />

      <DialogContent className="sm:max-w-md">
        {created === null ? (
          <form
            onSubmit={(event) => {
              void submit(event);
            }}
            noValidate
          >
            <DialogHeader>
              <DialogTitle>Ekip üyesi davet et</DialogTitle>
              <DialogDescription>
                Davet bağlantısı yalnızca bu adrese ait hesapla kullanılabilir ve 7 gün geçerlidir.
              </DialogDescription>
            </DialogHeader>

            <div className="mt-4 flex flex-col gap-4">
              <Field invalid={errors.email !== undefined}>
                <FieldLabel required>E-posta</FieldLabel>
                <FieldControl>
                  <Input type="email" placeholder="ad.soyad@sirket.com" {...register("email")} />
                </FieldControl>
                <FieldError>{errors.email?.message}</FieldError>
              </Field>

              <Field described>
                <FieldLabel required>Rol</FieldLabel>
                <FieldControl>
                  <Select
                    defaultValue="Agent"
                    onValueChange={(value) => {
                      setValue("role", value as MembershipRole);
                    }}
                  >
                    <SelectTrigger className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {offeredRoles.map((role) => (
                        <SelectItem key={role} value={role}>
                          {membershipRoleLabels[role].label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldControl>
                <FieldDescription>Rol sonradan değiştirilebilir.</FieldDescription>
              </Field>
            </div>

            <DialogFooter className="mt-5">
              <Button type="button" variant="ghost" onClick={close}>
                Vazgeç
              </Button>
              <Button type="submit" disabled={inviteMutation.isPending}>
                {inviteMutation.isPending ? "Oluşturuluyor…" : "Davet oluştur"}
              </Button>
            </DialogFooter>
          </form>
        ) : (
          <InvitationLink invitation={created} onDone={close} />
        )}
      </DialogContent>
    </Dialog>
  );
}

/**
 * Shows the invitation link once.
 *
 * Only a hash is stored, so this value cannot be shown again. E-mail delivery
 * arrives in Faz 12; until then an admin passes the link on themselves, and the
 * screen says so rather than letting them assume a message was sent.
 */
function InvitationLink({
  invitation,
  onDone,
}: {
  invitation: CreatedInvitation;
  onDone: () => void;
}) {
  const link = `${window.location.origin}/davet?token=${encodeURIComponent(invitation.token)}`;

  return (
    <>
      <DialogHeader>
        <DialogTitle>Davet bağlantısı hazır</DialogTitle>
        <DialogDescription>
          Bu bağlantı yalnızca bir kez gösterilir. {invitation.invitation.email} adresine iletin.
        </DialogDescription>
      </DialogHeader>

      <div className="mt-4 flex flex-col gap-3">
        <Alert variant="warning">
          <AlertDescription>
            E-posta gönderimi henüz devrede değil. Bağlantıyı kendiniz iletmeniz gerekiyor.
          </AlertDescription>
        </Alert>

        <div className="flex items-center gap-2">
          <Input readOnly value={link} className="font-mono text-xs" />
          <Button
            variant="outline"
            size="icon"
            aria-label="Bağlantıyı kopyala"
            onClick={() => {
              void navigator.clipboard.writeText(link).then(
                () => {
                  toast.success("Bağlantı kopyalandı");
                },
                () => {
                  toast.error("Bağlantı kopyalanamadı", {
                    description: "Metni elle seçip kopyalayabilirsiniz.",
                  });
                },
              );
            }}
          >
            <CopyIcon />
          </Button>
        </div>
      </div>

      <DialogFooter className="mt-5">
        <Button onClick={onDone}>Tamam</Button>
      </DialogFooter>
    </>
  );
}
