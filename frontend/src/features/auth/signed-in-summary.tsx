"use client";

import { LogOutIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/product/page-header";
import { RequireSession } from "./require-session";
import { useLogout } from "./auth-queries";

/**
 * Confirms the session survives a page reload, which is the thing worth
 * showing at this stage: the access token is gone from memory and the session
 * was rebuilt from the refresh cookie (ADR-0006).
 */
export function SignedInSummary() {
  const logoutMutation = useLogout();

  return (
    <RequireSession>
      {(user) => (
        <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-10">
          <PageHeader
            title="Genel bakış"
            description="Oturumunuz açık. Çalışma alanı yönetimi bir sonraki aşamada geliyor."
            actions={
              <Button
                variant="outline"
                size="sm"
                disabled={logoutMutation.isPending}
                onClick={() => {
                  logoutMutation.mutate();
                }}
              >
                <LogOutIcon />
                Çıkış yap
              </Button>
            }
          />

          <dl className="border-border bg-card mt-6 grid gap-4 rounded-xl border p-4 sm:grid-cols-2">
            <div>
              <dt className="text-muted-foreground text-xs font-medium">Ad soyad</dt>
              <dd className="text-foreground mt-0.5 text-sm">{user.displayName}</dd>
            </div>

            <div>
              <dt className="text-muted-foreground text-xs font-medium">E-posta</dt>
              <dd className="text-foreground mt-0.5 text-sm">{user.email}</dd>
            </div>

            <div className="sm:col-span-2">
              <dt className="text-muted-foreground text-xs font-medium">Kullanıcı kimliği</dt>
              <dd className="text-muted-foreground mt-0.5 font-mono text-xs">{user.id}</dd>
            </div>
          </dl>

          <p className="text-muted-foreground mt-4 text-sm">
            Sayfayı yenilediğinizde oturum açık kalır. Erişim anahtarı yalnızca bellekte tutulduğu
            için yenileme sırasında sunucudan yeniden alınır.
          </p>
        </main>
      )}
    </RequireSession>
  );
}
