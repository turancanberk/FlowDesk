"use client";

import Link from "next/link";
import {
  ActivityIcon,
  BuildingIcon,
  LayoutDashboardIcon,
  ListChecksIcon,
  LogOutIcon,
  SettingsIcon,
  TicketIcon,
  UsersIcon,
} from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { AppSidebar, type SidebarGroup } from "@/components/product/app-sidebar";
import { EmptyState } from "@/components/product/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { useLogout } from "@/features/auth/auth-queries";
import { RequireSession } from "@/features/auth/require-session";
import type { CurrentUser } from "@/features/auth/auth-types";
import type { Workspace } from "./workspace-types";
import { membershipRoleLabels } from "@/lib/domain-labels";
import { useWorkspace, useWorkspaces } from "./workspace-queries";
import { WorkspaceSwitcher } from "./workspace-switcher";

/**
 * The shell every workspace page renders inside: sidebar, workspace switcher
 * and user identity.
 *
 * Loading, missing-workspace and empty states are handled here so that each
 * page can assume it has a workspace to work with.
 */
export function WorkspaceShell({
  workspaceSlug,
  activeSection,
  children,
}: {
  workspaceSlug: string;
  activeSection: WorkspaceSection;
  /*
    A render function rather than plain nodes, so a page receives the resolved
    workspace — including the caller's own role — without fetching it a second
    time. The shell has already handled the loading and not-found states by the
    time this runs.
  */
  children: (workspace: Workspace) => React.ReactNode;
}) {
  return (
    <RequireSession>
      {(user) => (
        <WorkspaceShellContent
          workspaceSlug={workspaceSlug}
          activeSection={activeSection}
          user={user}
        >
          {children}
        </WorkspaceShellContent>
      )}
    </RequireSession>
  );
}

export type WorkspaceSection =
  "dashboard" | "customers" | "tickets" | "tasks" | "team" | "activity" | "settings";

function WorkspaceShellContent({
  workspaceSlug,
  activeSection,
  user,
  children,
}: {
  workspaceSlug: string;
  activeSection: WorkspaceSection;
  user: CurrentUser;
  children: (workspace: Workspace) => React.ReactNode;
}) {
  const workspaceQuery = useWorkspace(workspaceSlug);
  const workspacesQuery = useWorkspaces();

  if (workspaceQuery.isPending) {
    return <WorkspaceShellSkeleton />;
  }

  if (workspaceQuery.isError || workspaceQuery.data === undefined) {
    /*
      The API answers 404 both for a workspace that does not exist and for one
      the user does not belong to, and it does so deliberately (ADR-0007). The
      message here says the same thing for the same reason: it must not hint
      that the address belongs to somebody else's organisation.
    */
    return (
      <main className="flex flex-1 items-center justify-center px-4 py-16">
        <EmptyState
          icon={<BuildingIcon />}
          title="Çalışma alanı bulunamadı"
          description="Bu adres geçerli değil ya da erişiminiz yok. Kendi çalışma alanlarınızdan birini açabilirsiniz."
          action={
            <Button
              size="sm"
              nativeButton={false}
              render={(buttonProps) => <Link {...buttonProps} href="/" />}
            >
              Çalışma alanlarım
            </Button>
          }
        />
      </main>
    );
  }

  const workspace = workspaceQuery.data;
  const groups = buildSidebarGroups(workspace.slug);
  const activeHref = `/app/${workspace.slug}/${activeSection}`;

  return (
    <div className="flex min-h-full flex-1">
      <AppSidebar
        groups={groups}
        activeHref={activeHref}
        className="hidden md:flex"
        workspaceSlot={
          <WorkspaceSwitcher current={workspace} workspaces={workspacesQuery.data ?? [workspace]} />
        }
        userSlot={
          <UserIdentity user={user} roleLabel={membershipRoleLabels[workspace.role].label} />
        }
      />

      <div className="bg-canvas flex min-w-0 flex-1 flex-col">{children(workspace)}</div>
    </div>
  );
}

function UserIdentity({ user, roleLabel }: { user: CurrentUser; roleLabel: string }) {
  const logoutMutation = useLogout();

  return (
    <div className="flex items-center gap-2 px-1.5 py-1">
      <Avatar className="size-6">
        <AvatarFallback className="text-[10px]">{initials(user.displayName)}</AvatarFallback>
      </Avatar>

      <span className="min-w-0 flex-1">
        <span className="text-foreground block truncate text-xs font-medium">
          {user.displayName}
        </span>
        <span className="text-muted-foreground block truncate text-[11px]">{roleLabel}</span>
      </span>

      <Button
        variant="ghost"
        size="icon-sm"
        aria-label="Çıkış yap"
        disabled={logoutMutation.isPending}
        onClick={() => {
          logoutMutation.mutate();
        }}
      >
        <LogOutIcon />
      </Button>
    </div>
  );
}

function WorkspaceShellSkeleton() {
  return (
    <div className="flex min-h-full flex-1">
      <div className="border-sidebar-border bg-sidebar hidden w-60 shrink-0 flex-col gap-2 border-r p-3 md:flex">
        <Skeleton className="h-8 w-full" />
        <Skeleton className="mt-4 h-4 w-20" />
        {[0, 1, 2, 3].map((row) => (
          <Skeleton key={row} className="h-8 w-full" />
        ))}
      </div>

      <div className="bg-canvas flex-1 p-6">
        <Skeleton className="h-7 w-48" />
        <Skeleton className="mt-3 h-4 w-72" />
      </div>
    </div>
  );
}

function buildSidebarGroups(slug: string): SidebarGroup[] {
  const base = `/app/${slug}`;

  return [
    {
      label: "Genel Bakış",
      items: [{ href: `${base}/dashboard`, label: "Dashboard", icon: <LayoutDashboardIcon /> }],
    },
    {
      label: "Müşteri Operasyonu",
      items: [
        { href: `${base}/customers`, label: "Müşteriler", icon: <BuildingIcon /> },
        { href: `${base}/tickets`, label: "Talepler", icon: <TicketIcon /> },
        { href: `${base}/tasks`, label: "Görevler", icon: <ListChecksIcon /> },
      ],
    },
    {
      label: "Yönetim",
      items: [
        { href: `${base}/team`, label: "Ekip", icon: <UsersIcon /> },
        { href: `${base}/activity`, label: "Etkinlik", icon: <ActivityIcon /> },
      ],
    },
    {
      items: [{ href: `${base}/settings`, label: "Ayarlar", icon: <SettingsIcon /> }],
    },
  ];
}

function initials(displayName: string): string {
  return displayName
    .split(/\s+/)
    .filter((part) => part.length > 0)
    .slice(0, 2)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR") ?? "")
    .join("");
}
