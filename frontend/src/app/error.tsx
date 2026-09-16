"use client";

import { RotateCcwIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/product/empty-state";

/*
  Uygulama genelindeki hata sınırı.

  Next.js'in varsayılan hata ekranı İngilizcedir ve kullanıcıya ne
  yapabileceğini söylemez. Hata metni burada da gösterilmez: iç ayrıntı son
  kullanıcıya sızdırılmaz (docs/SECURITY.md), kayıt sunucu log'una düşer.
*/
export default function AppError({ reset }: { error: Error; reset: () => void }) {
  return (
    <main className="flex flex-1 items-center justify-center px-4 py-16">
      <EmptyState
        title="Bu sayfa yüklenemedi"
        description="Beklenmeyen bir sorun oluştu. Tekrar denemek sorunu çözebilir."
        action={
          <Button size="sm" onClick={reset}>
            <RotateCcwIcon />
            Tekrar dene
          </Button>
        }
      />
    </main>
  );
}
