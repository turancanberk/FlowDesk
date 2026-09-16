"use client";

import {
  ActivityIcon,
  BuildingIcon,
  ChevronsUpDownIcon,
  LayoutDashboardIcon,
  ListChecksIcon,
  PlusIcon,
  SettingsIcon,
  TicketIcon,
  UsersIcon,
} from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { AppSidebar, type SidebarGroup } from "@/components/product/app-sidebar";
import { PageHeader } from "@/components/product/page-header";
import { ShowcaseSection, Surface } from "../_components/showcase-section";

/*
  Gezinme: kenar çubuğu ve sayfa başlığı.
*/

const sidebarGroups: SidebarGroup[] = [
  {
    label: "Genel Bakış",
    items: [{ href: "/app/acme/dashboard", label: "Dashboard", icon: <LayoutDashboardIcon /> }],
  },
  {
    label: "Müşteri Operasyonu",
    items: [
      { href: "/app/acme/customers", label: "Müşteriler", icon: <BuildingIcon /> },
      { href: "/app/acme/tickets", label: "Talepler", icon: <TicketIcon />, count: 12 },
      { href: "/app/acme/tasks", label: "Görevler", icon: <ListChecksIcon />, count: 3 },
    ],
  },
  {
    label: "Yönetim",
    items: [
      { href: "/app/acme/team", label: "Ekip", icon: <UsersIcon /> },
      { href: "/app/acme/activity", label: "Etkinlik", icon: <ActivityIcon /> },
    ],
  },
  {
    items: [{ href: "/app/acme/settings", label: "Ayarlar", icon: <SettingsIcon /> }],
  },
];

function NavigationSections() {
  return (
    <>
      <ShowcaseSection
        id="sayfa-basligi"
        title="Sayfa başlığı"
        description="Başlık solda, birincil eylem sağda, altında ince ayraç. Başlığın yanına dekoratif ikon konmaz."
      >
        <Surface>
          <PageHeader
            title="Talepler"
            description="Açık ve devam eden destek taleplerini buradan yönetin."
            actions={
              <>
                <Button variant="outline" size="sm">
                  Dışa aktar
                </Button>
                <Button size="sm">
                  <PlusIcon />
                  Yeni talep
                </Button>
              </>
            }
          />
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="kenar-cubugu"
        title="Kenar çubuğu"
        description="Seçili durum bilinçli olarak sade: açık zemin, koyulaşan metin ve solda 2 px petrol şerit. Büyük yuvarlatılmış dolgu blokları kullanılmaz."
      >
        <div className="border-border overflow-hidden rounded-xl border">
          <div className="bg-card flex h-[32rem]">
            <AppSidebar
              groups={sidebarGroups}
              activeHref="/app/acme/tickets"
              workspaceSlot={
                <button
                  type="button"
                  className="hover:bg-sidebar-accent flex h-8 w-full items-center gap-2 rounded-md px-2 text-left text-sm transition-colors duration-100"
                >
                  <span className="bg-primary text-primary-foreground flex size-5 shrink-0 items-center justify-center rounded-sm font-mono text-[10px] font-medium">
                    AT
                  </span>
                  <span className="text-foreground truncate font-medium">Acme Teknoloji</span>
                  <ChevronsUpDownIcon className="text-muted-foreground ml-auto size-3.5 shrink-0" />
                </button>
              }
              userSlot={
                <button
                  type="button"
                  className="hover:bg-sidebar-accent flex h-9 w-full items-center gap-2 rounded-md px-1.5 text-left transition-colors duration-100"
                >
                  <Avatar className="size-6">
                    <AvatarFallback className="text-[10px]">CT</AvatarFallback>
                  </Avatar>
                  <span className="min-w-0 flex-1">
                    <span className="text-foreground block truncate text-xs font-medium">
                      Canberk Turan
                    </span>
                    <span className="text-muted-foreground block truncate text-[11px]">Sahip</span>
                  </span>
                  <ChevronsUpDownIcon className="text-muted-foreground size-3.5 shrink-0" />
                </button>
              }
            />

            <div className="bg-canvas flex-1 p-5">
              <PageHeader
                title="Talepler"
                description="Kenar çubuğu ile içerik alanının birlikte görünümü."
                actions={
                  <Button size="sm">
                    <PlusIcon />
                    Yeni talep
                  </Button>
                }
              />
              <p className="text-muted-foreground mt-4 text-sm">
                İçerik alanı uygulama zemini üzerinde durur; yüzeyler bu zeminden kenarlıkla
                ayrılır.
              </p>
            </div>
          </div>
        </div>
      </ShowcaseSection>
    </>
  );
}

export { NavigationSections };
