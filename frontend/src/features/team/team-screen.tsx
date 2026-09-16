"use client";

import { MoreHorizontalIcon, UsersIcon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuGroup,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { EmptyState } from "@/components/product/empty-state";
import { PageHeader } from "@/components/product/page-header";
import { MembershipRoleBadge } from "@/components/product/status-badge";
import { ApiError } from "@/lib/api/api-error";
import { formatDate, formatRelativeTime } from "@/lib/format";
import { membershipRoleLabels } from "@/lib/domain-labels";
import type { MembershipRole, Workspace } from "@/features/workspaces/workspace-types";
import { InviteMemberDialog } from "./invite-member-dialog";
import {
  useChangeMemberRole,
  useInvitations,
  useMembers,
  useRemoveMember,
  useRevokeInvitation,
} from "./team-queries";
import type { TeamMember } from "./team-types";

const MANAGEABLE_ROLES: MembershipRole[] = ["Viewer", "Agent", "Admin", "Owner"];

export function TeamScreen({ workspace }: { workspace: Workspace }) {
  const canManage = workspace.role === "Admin" || workspace.role === "Owner";

  return (
    <div className="px-6 py-6">
      <PageHeader
        title="Ekip"
        description="Çalışma alanındaki kişiler ve bekleyen davetler."
        actions={
          canManage ? (
            <InviteMemberDialog workspaceSlug={workspace.slug} callerRole={workspace.role} />
          ) : null
        }
      />

      <MembersSection workspace={workspace} canManage={canManage} />
      {canManage ? <InvitationsSection workspace={workspace} /> : null}
    </div>
  );
}

function MembersSection({ workspace, canManage }: { workspace: Workspace; canManage: boolean }) {
  const { data: members, isPending, isError, refetch } = useMembers(workspace.slug);

  if (isPending) {
    return <TableSkeleton />;
  }

  if (isError) {
    return (
      <SectionSurface>
        <EmptyState
          title="Ekip yüklenemedi"
          description="Bağlantı kurulamadı. Tekrar denemek sorunu çözebilir."
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
      </SectionSurface>
    );
  }

  return (
    <div className="border-border bg-card mt-6 overflow-hidden rounded-xl border">
      <Table>
        <TableHeader>
          <TableRow className="hover:bg-muted">
            <TableHead>Kişi</TableHead>
            <TableHead>Rol</TableHead>
            <TableHead>Katıldı</TableHead>
            {canManage ? <TableHead className="w-12" /> : null}
          </TableRow>
        </TableHeader>
        <TableBody>
          {members.map((member) => (
            <TableRow key={member.userId}>
              <TableCell>
                <span className="text-foreground block truncate font-medium">
                  {member.displayName}
                </span>
                <span className="text-muted-foreground block truncate text-xs">{member.email}</span>
              </TableCell>
              <TableCell>
                <MembershipRoleBadge role={member.role} />
              </TableCell>
              <TableCell className="text-muted-foreground">{formatDate(member.joinedAt)}</TableCell>
              {canManage ? (
                <TableCell className="text-right">
                  <MemberActions workspace={workspace} member={member} />
                </TableCell>
              ) : null}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function MemberActions({ workspace, member }: { workspace: Workspace; member: TeamMember }) {
  const changeRoleMutation = useChangeMemberRole(workspace.slug);
  const removeMutation = useRemoveMember(workspace.slug);

  const callerIsOwner = workspace.role === "Owner";

  /*
    An Admin may manage the team but not the people above them, and only an
    Owner can hand out ownership. The same rules are enforced on the server;
    disabling the controls here just avoids offering an action that would be
    rejected.
  */
  const canManageThisMember = callerIsOwner || member.role !== "Owner";

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon-sm" aria-label={`${member.displayName} için işlemler`}>
            <MoreHorizontalIcon />
          </Button>
        }
      />

      <DropdownMenuContent align="end" className="w-48">
        <DropdownMenuGroup>
          <DropdownMenuLabel>Rolü değiştir</DropdownMenuLabel>

          {MANAGEABLE_ROLES.filter((role) => callerIsOwner || role !== "Owner").map((role) => (
            <DropdownMenuItem
              key={role}
              disabled={
                !canManageThisMember ||
                role === member.role ||
                (member.isLastOwner && role !== "Owner")
              }
              onClick={() => {
                changeRoleMutation.mutate(
                  { userId: member.userId, role },
                  {
                    onSuccess: () => {
                      toast.success("Rol güncellendi", {
                        description: `${member.displayName} artık ${membershipRoleLabels[role].label}.`,
                      });
                    },
                    onError: (error) => {
                      toast.error("Rol değiştirilemedi", {
                        description:
                          error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                      });
                    },
                  },
                );
              }}
            >
              {membershipRoleLabels[role].label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuGroup>

        <DropdownMenuSeparator />

        <DropdownMenuItem
          variant="destructive"
          disabled={!canManageThisMember || member.isLastOwner}
          onClick={() => {
            removeMutation.mutate(member.userId, {
              onSuccess: () => {
                toast.success("Üye çıkarıldı", {
                  description: `${member.displayName} çalışma alanından çıkarıldı.`,
                });
              },
              onError: (error) => {
                toast.error("Üye çıkarılamadı", {
                  description: error instanceof ApiError ? error.message : "Lütfen tekrar deneyin.",
                });
              },
            });
          }}
        >
          Çalışma alanından çıkar
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function InvitationsSection({ workspace }: { workspace: Workspace }) {
  const { data: invitations, isPending } = useInvitations(workspace.slug);
  const revokeMutation = useRevokeInvitation(workspace.slug);

  if (isPending) {
    return null;
  }

  if (invitations === undefined || invitations.length === 0) {
    return (
      <section className="mt-8">
        <h2 className="text-foreground text-sm font-semibold">Bekleyen davetler</h2>
        <SectionSurface className="mt-3">
          <EmptyState
            icon={<UsersIcon />}
            title="Bekleyen davet yok"
            description="Ekibinizi büyütmek için yeni bir davet oluşturabilirsiniz."
          />
        </SectionSurface>
      </section>
    );
  }

  return (
    <section className="mt-8">
      <h2 className="text-foreground text-sm font-semibold">Bekleyen davetler</h2>

      <div className="border-border bg-card mt-3 overflow-hidden rounded-xl border">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-muted">
              <TableHead>E-posta</TableHead>
              <TableHead>Rol</TableHead>
              <TableHead>Geçerlilik</TableHead>
              <TableHead className="w-24" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {invitations.map((invitation) => (
              <TableRow key={invitation.id}>
                <TableCell className="text-foreground font-medium">{invitation.email}</TableCell>
                <TableCell>
                  <MembershipRoleBadge role={invitation.role} />
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatRelativeTime(invitation.expiresAt)} doluyor
                </TableCell>
                <TableCell className="text-right">
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={revokeMutation.isPending}
                    onClick={() => {
                      revokeMutation.mutate(invitation.id, {
                        onSuccess: () => {
                          toast.success("Davet iptal edildi");
                        },
                        onError: () => {
                          toast.error("Davet iptal edilemedi");
                        },
                      });
                    }}
                  >
                    İptal et
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </section>
  );
}

function SectionSurface({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={`border-border bg-card overflow-hidden rounded-xl border ${className ?? "mt-6"}`}
    >
      {children}
    </div>
  );
}

function TableSkeleton() {
  return (
    <div className="border-border bg-card mt-6 flex flex-col gap-2 rounded-xl border p-4">
      {[0, 1, 2].map((row) => (
        <div key={row} className="flex items-center gap-4">
          <Skeleton className="h-4 flex-1" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-24" />
        </div>
      ))}
    </div>
  );
}
