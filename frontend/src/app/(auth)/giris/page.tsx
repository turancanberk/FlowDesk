import { Suspense } from "react";
import type { Metadata } from "next";
import { AuthCard } from "@/features/auth/auth-card";
import { LoginForm } from "@/features/auth/login-form";
import { Skeleton } from "@/components/ui/skeleton";

export const metadata: Metadata = {
  title: "Giriş yap",
};

export default function LoginPage() {
  return (
    <AuthCard
      title="Giriş yap"
      description="Çalışma alanlarınıza erişmek için hesabınıza giriş yapın."
      footer={{ question: "Hesabınız yok mu?", linkLabel: "Hesap oluşturun", href: "/kayit" }}
    >
      {/*
        The form reads the `next` query parameter to return an invited user to
        their invitation after signing in. Reading search params opts a client
        component out of static prerendering unless it sits behind a Suspense
        boundary, so the page keeps its static shell and only the form waits.
      */}
      <Suspense fallback={<FormSkeleton />}>
        <LoginForm />
      </Suspense>
    </AuthCard>
  );
}

function FormSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <Skeleton className="h-14 w-full" />
      <Skeleton className="h-14 w-full" />
      <Skeleton className="h-9 w-full" />
    </div>
  );
}
