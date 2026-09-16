"use client";

import Link from "next/link";
import { BuildingIcon, LogOutIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/product/empty-state";
import { PageHeader } from "@/components/product/page-header";
import { MembershipRoleBadge } from "@/components/product/status-badge";
import { useLogout } from "@/features/auth/auth-queries";
import { RequireSession } from "@/features/auth/require-session";
import { formatDate } from "@/lib/format";
import { useWorkspaces } from "./workspace-queries";

/** The workspaces the signed-in user belongs to, and a way into each. */
export function WorkspaceList() {
  return (
    <RequireSession>
      {(user) => (
        <main className="mx-auto w-full max-w-3xl flex-1 px-4 py-10">
          <WorkspaceListHeader displayName={user.displayName} />
          <WorkspaceListBody />
        </main>
      )}
    </RequireSession>
  );
}

function WorkspaceListHeader({ displayName }: { displayName: string }) {
  const logoutMutation = useLogout();

  return (
    <PageHeader
      title="Çalışma alanları"
      description={`Hoş geldiniz, ${displayName}. Bir çalışma alanı seçin veya yenisini oluşturun.`}
      actions={
        <>
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

          <Button
            size="sm"
            nativeButton={false}
            render={(buttonProps) => <Link {...buttonProps} href="/app/yeni" />}
          >
            <PlusIcon />
            Yeni çalışma alanı
          </Button>
        </>
      }
    />
  );
}

function WorkspaceListBody() {
  const { data: workspaces, isPending, isError, refetch } = useWorkspaces();

  if (isPending) {
    return (
      <div className="mt-6 flex flex-col gap-2">
        {[0, 1, 2].map((row) => (
          <Skeleton key={row} className="h-16 w-full rounded-xl" />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="border-border bg-card mt-6 rounded-xl border">
        <EmptyState
          title="Çalışma alanları yüklenemedi"
          description="Bağlantı kurulamadı. Tekrar denemek için aşağıdaki düğmeyi kullanın."
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
      </div>
    );
  }

  if (workspaces.length === 0) {
    return (
      <div className="border-border bg-card mt-6 rounded-xl border">
        <EmptyState
          icon={<BuildingIcon />}
          title="Henüz çalışma alanınız yok"
          description="İlk çalışma alanınızı oluşturduğunuzda ekibinizi davet edebilir ve müşteri kayıtlarını yönetmeye başlayabilirsiniz."
          action={
            <Button
              size="sm"
              nativeButton={false}
              render={(buttonProps) => <Link {...buttonProps} href="/app/yeni" />}
            >
              <PlusIcon />
              Yeni çalışma alanı
            </Button>
          }
        />
      </div>
    );
  }

  return (
    <ul className="mt-6 flex flex-col gap-2">
      {workspaces.map((workspace) => (
        <li key={workspace.id}>
          <Link
            href={`/app/${workspace.slug}/dashboard`}
            className="border-border bg-card hover:bg-muted flex items-center gap-3 rounded-xl border px-4 py-3 transition-colors duration-100"
          >
            <span className="min-w-0 flex-1">
              <span className="text-foreground block truncate text-sm font-medium">
                {workspace.name}
              </span>
              <span className="text-muted-foreground block truncate font-mono text-xs">
                /app/{workspace.slug}
              </span>
            </span>

            <MembershipRoleBadge role={workspace.role} />

            <span className="text-muted-foreground hidden text-xs sm:block">
              {formatDate(workspace.createdAt)}
            </span>
          </Link>
        </li>
      ))}
    </ul>
  );
}
