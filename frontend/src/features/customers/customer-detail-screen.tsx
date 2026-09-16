"use client";

import * as React from "react";
import Link from "next/link";
import { ArchiveIcon, ArrowLeftIcon, PencilIcon, RotateCcwIcon } from "lucide-react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { EmptyState } from "@/components/product/empty-state";
import { PageHeader } from "@/components/product/page-header";
import { CustomerStatusBadge, StatusBadge } from "@/components/product/status-badge";
import { formatDateTime } from "@/lib/format";
import { ApiError } from "@/lib/api/api-error";
import type { Workspace } from "@/features/workspaces/workspace-types";
import { CustomerTicketsPanel } from "@/features/tickets/customer-tickets-panel";
import { CustomerFormDialog } from "./customer-form-dialog";
import { useArchiveCustomer, useCustomer, useRestoreCustomer } from "./customer-queries";
import type { CustomerDetail } from "./customer-types";

export function CustomerDetailScreen({
  workspace,
  customerId,
}: {
  workspace: Workspace;
  customerId: string;
}) {
  const { data: customer, isPending, isError, error } = useCustomer(workspace.slug, customerId);

  if (isPending) {
    return <DetailSkeleton />;
  }

  if (isError || customer === undefined) {
    return (
      <div className="px-6 py-6">
        <EmptyState
          title="Müşteri bulunamadı"
          description={
            error instanceof ApiError
              ? error.message
              : "Bu kayda erişilemiyor ya da kaldırılmış olabilir."
          }
          action={
            <Button
              size="sm"
              nativeButton={false}
              render={(props) => <Link {...props} href={`/app/${workspace.slug}/customers`} />}
            >
              Müşteri listesi
            </Button>
          }
        />
      </div>
    );
  }

  return <CustomerDetailContent workspace={workspace} customer={customer} />;
}

function CustomerDetailContent({
  workspace,
  customer,
}: {
  workspace: Workspace;
  customer: CustomerDetail;
}) {
  const [isEditing, setIsEditing] = React.useState(false);

  const archiveMutation = useArchiveCustomer(workspace.slug);
  const restoreMutation = useRestoreCustomer(workspace.slug);

  const canManage = workspace.role !== "Viewer";
  const canArchive = workspace.role === "Admin" || workspace.role === "Owner";

  return (
    <div className="px-6 py-6">
      <Link
        href={`/app/${workspace.slug}/customers`}
        className="text-muted-foreground hover:text-foreground mb-4 inline-flex items-center gap-1.5 rounded-sm text-sm transition-colors duration-100"
      >
        <ArrowLeftIcon className="size-3.5" />
        Müşteriler
      </Link>

      <PageHeader
        title={customer.name}
        description={customer.company ?? undefined}
        actions={
          canManage ? (
            <>
              {customer.isArchived ? (
                <Button
                  size="sm"
                  variant="outline"
                  disabled={!canArchive || restoreMutation.isPending}
                  onClick={() => {
                    restoreMutation.mutate(customer.id, {
                      onSuccess: () => {
                        toast.success("Müşteri arşivden çıkarıldı");
                      },
                      onError: () => {
                        toast.error("Arşivden çıkarılamadı");
                      },
                    });
                  }}
                >
                  <RotateCcwIcon />
                  Arşivden çıkar
                </Button>
              ) : (
                <>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={!canArchive || archiveMutation.isPending}
                    onClick={() => {
                      archiveMutation.mutate(customer.id, {
                        onSuccess: () => {
                          toast.success("Müşteri arşivlendi", {
                            description: "Kayıt listeden kaldırıldı, geçmişi korundu.",
                          });
                        },
                        onError: () => {
                          toast.error("Müşteri arşivlenemedi");
                        },
                      });
                    }}
                  >
                    <ArchiveIcon />
                    Arşivle
                  </Button>

                  <Button
                    size="sm"
                    onClick={() => {
                      setIsEditing(true);
                    }}
                  >
                    <PencilIcon />
                    Düzenle
                  </Button>
                </>
              )}
            </>
          ) : null
        }
      />

      {customer.isArchived ? (
        <Alert variant="warning" className="mt-4">
          <AlertDescription>
            Bu müşteri arşivlenmiş. Listelerde görünmüyor, ancak geçmiş talep ve görevleri
            korunuyor.
          </AlertDescription>
        </Alert>
      ) : null}

      <Tabs defaultValue="genel" className="mt-6">
        <TabsList>
          <TabsTrigger value="genel">Genel bakış</TabsTrigger>
          <TabsTrigger value="talepler">Talepler</TabsTrigger>
          <TabsTrigger value="gorevler">Görevler</TabsTrigger>
        </TabsList>

        <TabsContent value="genel" className="pt-5">
          <CustomerOverview customer={customer} />
        </TabsContent>

        <TabsContent value="talepler" className="pt-5">
          <div className="flex flex-col gap-3">
            <CustomerTicketsPanel
              workspace={workspace}
              customerId={customer.id}
              canManage={canManage}
            />
          </div>
        </TabsContent>

        <TabsContent value="gorevler" className="pt-5">
          <Surface>
            <EmptyState
              title="Görevler yakında"
              description="Görev yönetimi eklendiğinde bu müşteriye bağlı görevler burada listelenecek."
            />
          </Surface>
        </TabsContent>
      </Tabs>

      <CustomerFormDialog
        workspaceSlug={workspace.slug}
        customer={customer}
        open={isEditing}
        onOpenChange={setIsEditing}
      />
    </div>
  );
}

function CustomerOverview({ customer }: { customer: CustomerDetail }) {
  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Surface className="lg:col-span-2">
        <dl className="divide-border divide-y">
          <DetailRow label="Durum">
            <span className="flex items-center gap-1.5">
              <CustomerStatusBadge status={customer.status} />
              {customer.isArchived ? <StatusBadge tone="neutral">Arşivli</StatusBadge> : null}
            </span>
          </DetailRow>
          <DetailRow label="Şirket">{customer.company ?? "—"}</DetailRow>
          <DetailRow label="E-posta">
            {customer.email === null ? (
              "—"
            ) : (
              <a
                href={`mailto:${customer.email}`}
                className="text-primary rounded-sm hover:underline"
              >
                {customer.email}
              </a>
            )}
          </DetailRow>
          <DetailRow label="Telefon">{customer.phone ?? "—"}</DetailRow>
          <DetailRow label="Oluşturuldu">{formatDateTime(customer.createdAt)}</DetailRow>
          <DetailRow label="Güncellendi">{formatDateTime(customer.updatedAt)}</DetailRow>
        </dl>
      </Surface>

      <Surface>
        <div className="p-4">
          <h2 className="text-foreground text-sm font-semibold">Notlar</h2>
          {customer.notes === null ? (
            <p className="text-muted-foreground mt-2 text-sm">Bu müşteri için not eklenmemiş.</p>
          ) : (
            <p className="text-foreground mt-2 text-sm whitespace-pre-wrap">{customer.notes}</p>
          )}
        </div>
      </Surface>
    </div>
  );
}

function DetailRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[8rem_1fr] items-center gap-4 px-4 py-2.5">
      <dt className="text-muted-foreground text-xs">{label}</dt>
      <dd className="text-foreground text-sm">{children}</dd>
    </div>
  );
}

function Surface({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`border-border bg-card overflow-hidden rounded-xl border ${className ?? ""}`}>
      {children}
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="px-6 py-6">
      <Skeleton className="h-4 w-24" />
      <Skeleton className="mt-4 h-7 w-64" />
      <Skeleton className="mt-2 h-4 w-48" />
      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <Skeleton className="h-64 lg:col-span-2" />
        <Skeleton className="h-64" />
      </div>
    </div>
  );
}
