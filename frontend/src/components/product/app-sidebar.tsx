"use client";

import Link from "next/link";
import { cn } from "cn";

/*
  Uygulama kenar çubuğu.

  Seçili durum bilinçli olarak sade: açık zemin, koyulaşan metin ve solda 2px
  petrol şerit. Büyük yuvarlatılmış dolgu blokları kullanılmaz
  (docs/DESIGN_SYSTEM.md bölüm 11).

  Bu bileşen yalnızca sunum yapar. Çalışma alanı seçimi, üyelik ve yetki
  bilgisi Faz 04'te üstteki katmandan prop olarak gelir; kenar çubuğu veri
  çekmez.
*/

export type SidebarItem = {
  href: string;
  label: string;
  icon: React.ReactNode;
  /** Okunmamış bildirim gibi sayılabilir bir işaret. */
  count?: number;
};

export type SidebarGroup = {
  /** Grup başlığı. Tek öğelik gruplarda boş bırakılabilir. */
  label?: string;
  items: SidebarItem[];
};

type AppSidebarProps = {
  groups: SidebarGroup[];
  /** Geçerli rota; seçili öğeyi belirler. */
  activeHref: string;
  /** Çalışma alanı seçici. Faz 04'te gerçek bileşenle doldurulur. */
  workspaceSlot?: React.ReactNode;
  /** Kullanıcı satırının üstünde duran bildirim kontrolü. */
  notificationSlot?: React.ReactNode;
  /** Alt kısımdaki kullanıcı kimliği. */
  userSlot?: React.ReactNode;
  className?: string;
};

function AppSidebar({
  groups,
  activeHref,
  workspaceSlot,
  notificationSlot,
  userSlot,
  className,
}: AppSidebarProps) {
  return (
    <nav
      aria-label="Ana gezinme"
      className={cn(
        "border-sidebar-border bg-sidebar flex w-60 shrink-0 flex-col border-r",
        className,
      )}
    >
      <div className="h-topbar border-sidebar-border flex items-center border-b px-3">
        <span className="text-foreground font-mono text-xs font-medium tracking-widest uppercase">
          FlowDesk
        </span>
      </div>

      {workspaceSlot ? (
        <div className="border-sidebar-border border-b p-2">{workspaceSlot}</div>
      ) : null}

      <div className="flex-1 overflow-y-auto p-2">
        {groups.map((group, groupIndex) => (
          <div key={group.label ?? `group-${String(groupIndex)}`} className="mb-4 last:mb-0">
            {group.label ? (
              <h2 className="text-muted-foreground px-2 pb-1 text-[11px] font-semibold tracking-[0.04em] uppercase">
                {group.label}
              </h2>
            ) : null}

            <ul className="flex flex-col gap-px">
              {group.items.map((item) => {
                const isActive = item.href === activeHref;

                return (
                  <li key={item.href}>
                    <Link
                      href={item.href}
                      aria-current={isActive ? "page" : undefined}
                      className={cn(
                        "relative flex h-8 items-center gap-2 rounded-md pr-2 pl-3 text-sm transition-colors duration-100",
                        "[&_svg]:size-4 [&_svg]:shrink-0",
                        isActive
                          ? "bg-sidebar-accent text-sidebar-accent-foreground font-medium"
                          : "text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground",
                      )}
                    >
                      {isActive ? (
                        <span
                          aria-hidden="true"
                          className="bg-sidebar-primary absolute top-1 bottom-1 left-0 w-0.5 rounded-full"
                        />
                      ) : null}

                      <span className="text-muted-foreground">{item.icon}</span>
                      <span className="truncate">{item.label}</span>

                      {item.count !== undefined && item.count > 0 ? (
                        <span className="bg-muted text-muted-foreground ml-auto rounded-full px-1.5 font-mono text-[11px] tabular-nums">
                          {item.count}
                        </span>
                      ) : null}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </div>

      {notificationSlot ? (
        <div className="border-sidebar-border border-t p-2">{notificationSlot}</div>
      ) : null}

      {userSlot ? <div className="border-sidebar-border border-t p-2">{userSlot}</div> : null}
    </nav>
  );
}

export { AppSidebar };
