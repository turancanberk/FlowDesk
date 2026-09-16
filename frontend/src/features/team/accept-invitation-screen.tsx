"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CircleAlertIcon, Loader2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/product/empty-state";
import { ApiError } from "@/lib/api/api-error";
import { useCurrentUser } from "@/features/auth/auth-queries";
import { useAcceptInvitation } from "./team-queries";

/**
 * Redeems an invitation link.
 *
 * Accepting requires a signed-in account, because the invitation is bound to
 * the address it was sent to. A signed-out visitor is sent to sign in first and
 * returned here afterwards, with the token preserved.
 */
export function AcceptInvitationScreen({ token }: { token: string | null }) {
  const router = useRouter();
  const { data: user, isPending: sessionPending } = useCurrentUser();
  const acceptMutation = useAcceptInvitation();

  const hasToken = token !== null && token.length > 0;
  const signedIn = user !== null && user !== undefined;

  /*
    The single side effect on this screen: redeem the token once the session is
    known. It is an effect rather than a click handler because arriving on this
    page IS the action — asking the visitor to press a second button after
    following an invitation link would be ceremony.

    `isIdle` keeps it to one attempt: once the mutation has started, the
    condition is false and a re-render cannot fire it again. TanStack Query
    keeps `mutate` referentially stable, so it is safe as a dependency.
  */
  const { mutate: accept, isIdle } = acceptMutation;
  const shouldAccept = hasToken && signedIn && isIdle;

  React.useEffect(() => {
    if (shouldAccept && token !== null) {
      accept(token);
    }
  }, [shouldAccept, token, accept]);

  if (!hasToken) {
    return (
      <Centered>
        <EmptyState
          icon={<CircleAlertIcon />}
          title="Davet bağlantısı eksik"
          description="Bağlantı tam kopyalanmamış olabilir. Sizi davet eden kişiden yeni bir bağlantı isteyin."
          action={
            <Button size="sm" nativeButton={false} render={(p) => <Link {...p} href="/" />}>
              Ana sayfa
            </Button>
          }
        />
      </Centered>
    );
  }

  if (sessionPending) {
    return <Pending label="Oturum kontrol ediliyor" />;
  }

  if (!signedIn) {
    const next = encodeURIComponent(`/davet?token=${encodeURIComponent(token)}`);

    return (
      <Centered>
        <EmptyState
          title="Daveti kabul etmek için giriş yapın"
          description="Davet, gönderildiği e-posta adresine ait hesapla kabul edilebilir."
          action={
            <div className="flex gap-2">
              <Button
                size="sm"
                nativeButton={false}
                render={(p) => <Link {...p} href={`/giris?next=${next}`} />}
              >
                Giriş yap
              </Button>
              <Button
                size="sm"
                variant="outline"
                nativeButton={false}
                render={(p) => <Link {...p} href={`/kayit?next=${next}`} />}
              >
                Hesap oluştur
              </Button>
            </div>
          }
        />
      </Centered>
    );
  }

  if (acceptMutation.isSuccess) {
    const workspace = acceptMutation.data;

    return (
      <Centered>
        <EmptyState
          title={`${workspace.workspaceName} çalışma alanına katıldınız`}
          description="Artık ekibin bir parçasısınız. Çalışma alanını açarak başlayabilirsiniz."
          action={
            <Button
              size="sm"
              onClick={() => {
                router.replace(`/app/${workspace.workspaceSlug}/dashboard`);
              }}
            >
              Çalışma alanını aç
            </Button>
          }
        />
      </Centered>
    );
  }

  if (acceptMutation.isError) {
    return (
      <Centered>
        <EmptyState
          icon={<CircleAlertIcon />}
          title="Davet kullanılamadı"
          description={
            acceptMutation.error instanceof ApiError
              ? acceptMutation.error.message
              : "Beklenmeyen bir sorun oluştu. Lütfen tekrar deneyin."
          }
          action={
            <Button size="sm" nativeButton={false} render={(p) => <Link {...p} href="/" />}>
              Çalışma alanlarım
            </Button>
          }
        />
      </Centered>
    );
  }

  return <Pending label="Davet kabul ediliyor" />;
}

function Centered({ children }: { children: React.ReactNode }) {
  return <main className="flex flex-1 items-center justify-center px-4 py-16">{children}</main>;
}

function Pending({ label }: { label: string }) {
  return (
    <main
      className="flex flex-1 items-center justify-center px-4 py-16"
      role="status"
      aria-live="polite"
    >
      <Loader2Icon className="text-muted-foreground size-4 animate-spin" aria-hidden="true" />
      <span className="sr-only">{label}</span>
    </main>
  );
}
