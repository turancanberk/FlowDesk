"use client";

import {
  CircleCheckIcon,
  InfoIcon,
  Loader2Icon,
  OctagonXIcon,
  TriangleAlertIcon,
} from "lucide-react";
import { Toaster as Sonner, type ToasterProps } from "sonner";

/*
  Bildirim (toast) katmanı.

  shadcn'in ürettiği sürüm tema algılaması için next-themes kullanıyordu.
  Koyu tema çekirdek kapsam dışında (docs/ROADMAP.md), bu yüzden bağımlılık
  kaldırıldı ve tema açıkça "light" olarak sabitlendi. İkinci bir tema
  eklendiğinde burası tek dokunulacak yer olur.
*/
function Toaster(props: ToasterProps) {
  return (
    <Sonner
      theme="light"
      position="bottom-right"
      /*
        The region's name is read out by screen readers; sonner's default is
        English. Not "Bildirimler": that is the notification panel's button,
        and two landmarks with one name cannot be told apart.
      */
      containerAriaLabel="Anlık mesajlar"
      className="toaster group"
      icons={{
        success: <CircleCheckIcon className="size-4" />,
        info: <InfoIcon className="size-4" />,
        warning: <TriangleAlertIcon className="size-4" />,
        error: <OctagonXIcon className="size-4" />,
        loading: <Loader2Icon className="size-4 animate-spin" />,
      }}
      style={
        {
          "--normal-bg": "var(--popover)",
          "--normal-text": "var(--popover-foreground)",
          "--normal-border": "var(--border)",
          "--border-radius": "var(--radius)",
        } as React.CSSProperties
      }
      toastOptions={{ classNames: { toast: "cn-toast" } }}
      {...props}
    />
  );
}

export { Toaster };
