import type { Metadata } from "next";
import { AuthCard } from "@/features/auth/auth-card";
import { LoginForm } from "@/features/auth/login-form";

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
      <LoginForm />
    </AuthCard>
  );
}
