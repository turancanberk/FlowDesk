"use client";

import Link from "next/link";
import { CheckIcon, ChevronsUpDownIcon, PlusIcon } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { membershipRoleLabels } from "@/lib/domain-labels";
import type { Workspace } from "./workspace-types";

/**
 * Switches between the workspaces the signed-in user belongs to.
 *
 * The list comes from the server and contains only their own memberships, so
 * this control cannot reveal that another organisation exists.
 *
 * Menu entries are links rather than buttons: a teammate should be able to
 * middle-click a workspace or copy its address. Base UI is told so through
 * `nativeButton={false}`, which keeps it from warning that button semantics
 * were lost — here they are given up deliberately.
 */
export function WorkspaceSwitcher({
  current,
  workspaces,
}: {
  current: Workspace;
  workspaces: Workspace[];
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <button
            type="button"
            className="hover:bg-sidebar-accent flex h-8 w-full items-center gap-2 rounded-md px-2 text-left text-sm transition-colors duration-100"
          >
            <WorkspaceMonogram name={current.name} />
            <span className="text-foreground truncate font-medium">{current.name}</span>
            <ChevronsUpDownIcon className="text-muted-foreground ml-auto size-3.5 shrink-0" />
          </button>
        }
      />

      <DropdownMenuContent align="start" className="w-60">
        {/*
          The label sits inside a group because Base UI reads the group context
          to wire it up as that list's accessible name; a bare label outside one
          has nothing to describe and throws.
        */}
        <DropdownMenuGroup>
          <DropdownMenuLabel>Çalışma alanları</DropdownMenuLabel>

          {workspaces.map((workspace) => (
            <DropdownMenuItem
              key={workspace.id}
              nativeButton={false}
              render={(itemProps) => (
                <Link {...itemProps} href={`/app/${workspace.slug}/dashboard`} />
              )}
            >
              <WorkspaceMonogram name={workspace.name} />
              <span className="min-w-0 flex-1">
                <span className="block truncate">{workspace.name}</span>
                <span className="text-muted-foreground block truncate text-xs">
                  {membershipRoleLabels[workspace.role].label}
                </span>
              </span>
              {workspace.id === current.id ? (
                <CheckIcon className="text-primary size-3.5 shrink-0" />
              ) : null}
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>

        <DropdownMenuSeparator />

        <DropdownMenuItem
          nativeButton={false}
          render={(itemProps) => <Link {...itemProps} href="/app/yeni" />}
        >
          <PlusIcon />
          Yeni çalışma alanı
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/**
 * Two-letter monogram. Uses the Turkish locale so that "İzmir" yields "İZ"
 * rather than the dotless form invariant uppercasing would produce.
 */
function WorkspaceMonogram({ name }: { name: string }) {
  const monogram = name
    .split(/\s+/)
    .filter((part) => part.length > 0)
    .slice(0, 2)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR") ?? "")
    .join("");

  return (
    <span
      aria-hidden="true"
      className="bg-primary text-primary-foreground flex size-5 shrink-0 items-center justify-center rounded-sm font-mono text-[10px] font-medium"
    >
      {monogram}
    </span>
  );
}
