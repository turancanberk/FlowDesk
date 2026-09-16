import { Suspense } from "react";
import type { Metadata } from "next";
import { AuthCard } from "@/features/auth/auth-card";
import { RegisterForm } from "@/features/auth/register-form";
import { Skeleton } from "@/components/ui/skeleton";

export const metadata: Metadata = {
  title: "Hesap oluştur",
};

export default function RegisterPage() {
  return (
    <AuthCard
      title="Hesap oluştur"
      description="Birkaç saniyede hesabınızı açın ve ilk çalışma alanınızı kurun."
      footer={{ question: "Zaten hesabınız var mı?", linkLabel: "Giriş yapın", href: "/giris" }}
    >
      {/* See the sign-in page: the `next` parameter needs a Suspense boundary. */}
      <Suspense fallback={<FormSkeleton />}>
        <RegisterForm />
      </Suspense>
    </AuthCard>
  );
}

function FormSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <Skeleton className="h-14 w-full" />
      <Skeleton className="h-14 w-full" />
      <Skeleton className="h-16 w-full" />
      <Skeleton className="h-9 w-full" />
    </div>
  );
}
