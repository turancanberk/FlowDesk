"use client";

import Link from "next/link";
import { RequireSession } from "@/features/auth/require-session";
import { CreateWorkspaceForm } from "./create-workspace-form";

export function NewWorkspaceScreen() {
  return (
    <RequireSession>
      {() => (
        <main className="flex flex-1 items-center justify-center px-4 py-12">
          <div className="w-full max-w-sm">
            <p className="text-muted-foreground font-mono text-[11px] tracking-widest uppercase">
              FlowDesk
            </p>

            <h1 className="text-foreground mt-2 text-xl font-semibold">Yeni çalışma alanı</h1>
            <p className="text-muted-foreground mt-1 text-sm">
              Ekibinizin müşterilerini, taleplerini ve görevlerini yöneteceği alanı oluşturun.
            </p>

            <div className="border-border bg-card mt-6 rounded-xl border p-5">
              <CreateWorkspaceForm />
            </div>

            <p className="text-muted-foreground mt-4 text-center text-sm">
              <Link
                href="/"
                className="text-primary rounded-sm font-medium underline-offset-4 hover:underline"
              >
                Çalışma alanlarıma dön
              </Link>
            </p>
          </div>
        </main>
      )}
    </RequireSession>
  );
}
