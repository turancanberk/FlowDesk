import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { EmptyState } from "@/components/product/empty-state";

/*
  Bu bir Server Component. Buton yerine doğrudan bağlantı kullanılıyor ve
  görünümü buttonVariants ile alınıyor: Server Component'tan Client
  Component'a fonksiyon prop'u (render) geçirilemez, ve bu ekranın zaten
  istemci davranışına ihtiyacı yok.
*/
export default function NotFound() {
  return (
    <main className="flex flex-1 items-center justify-center px-4 py-16">
      <EmptyState
        title="Sayfa bulunamadı"
        description="Aradığınız adres taşınmış veya hiç var olmamış olabilir."
        action={
          <Link href="/" className={buttonVariants({ size: "sm" })}>
            Çalışma alanlarım
          </Link>
        }
      />
    </main>
  );
}
