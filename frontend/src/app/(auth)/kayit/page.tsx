import type { Metadata } from "next";
import { AuthCard } from "@/features/auth/auth-card";
import { RegisterForm } from "@/features/auth/register-form";

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
      <RegisterForm />
    </AuthCard>
  );
}
