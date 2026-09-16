"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { Loader2Icon } from "lucide-react";
import { useCurrentUser } from "./auth-queries";
import type { CurrentUser } from "./auth-types";

/*
  Client-side route guard.

  This is a user-experience layer, not a security boundary. It stops a signed-out
  visitor from staring at an empty shell; it does not protect data. Every
  protected response is authorised by the backend on its own, and a user who
  edits the URL or disables JavaScript gains nothing (ADR-0009).
*/

type RequireSessionProps = {
  children: (user: CurrentUser) => React.ReactNode;
};

export function RequireSession({ children }: RequireSessionProps) {
  const router = useRouter();
  const { data: user, isPending } = useCurrentUser();

  const shouldRedirect = !isPending && (user === null || user === undefined);

  React.useEffect(() => {
    if (shouldRedirect) {
      router.replace("/giris");
    }
  }, [shouldRedirect, router]);

  if (isPending) {
    return (
      <div
        className="flex flex-1 items-center justify-center py-16"
        role="status"
        aria-live="polite"
      >
        <Loader2Icon className="text-muted-foreground size-4 animate-spin" aria-hidden="true" />
        <span className="sr-only">Oturum kontrol ediliyor</span>
      </div>
    );
  }

  if (user === null || user === undefined) {
    return null;
  }

  return <>{children(user)}</>;
}
