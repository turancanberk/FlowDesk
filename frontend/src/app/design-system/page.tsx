import type { Metadata } from "next";
import { ControlsSections } from "./_sections/controls";
import { DataSections } from "./_sections/data";
import { FeedbackSections } from "./_sections/feedback";
import { FoundationsSections } from "./_sections/foundations";
import { NavigationSections } from "./_sections/navigation";

/*
  Tasarım sistemi vitrini.

  Bu sayfa bir bileşen galerisi değil, kararların kaydıdır: her bölüm neyin
  neden öyle olduğunu da söyler. İş sayfaları bu bileşenleri yeniden kullanır;
  her sayfada yeni bir tasarım icat edilmez.

  Kararların tamamı docs/DESIGN_SYSTEM.md içindedir.
*/

export const metadata: Metadata = {
  title: "Tasarım sistemi",
  description: "FlowDesk arayüz token'ları, bileşenleri ve kullanım kuralları.",
};

const tableOfContents = [
  { id: "tipografi", label: "Tipografi" },
  { id: "renk", label: "Renk" },
  { id: "olculer", label: "Ölçüler" },
  { id: "butonlar", label: "Butonlar" },
  { id: "girdiler", label: "Girdiler" },
  { id: "formlar", label: "Form alanı" },
  { id: "durumlar", label: "Durum göstergeleri" },
  { id: "rozetler", label: "Rozetler" },
  { id: "uyarilar", label: "Uyarılar" },
  { id: "bildirimler", label: "Bildirimler" },
  { id: "katmanlar", label: "Menü, diyalog, ipucu" },
  { id: "tablolar", label: "Tablolar" },
  { id: "sekmeler", label: "Sekmeler" },
  { id: "durumlar-async", label: "Yükleniyor ve boş durumlar" },
  { id: "sayfa-basligi", label: "Sayfa başlığı" },
  { id: "kenar-cubugu", label: "Kenar çubuğu" },
];

export default function DesignSystemPage() {
  return (
    <div className="mx-auto w-full max-w-6xl flex-1 px-4 py-10 lg:px-8">
      <header className="border-border border-b pb-6">
        <p className="text-muted-foreground font-mono text-[11px] tracking-widest uppercase">
          FlowDesk
        </p>
        <h1 className="text-foreground mt-2 text-xl font-semibold">Tasarım sistemi</h1>
        <p className="text-muted-foreground mt-2 max-w-2xl text-sm">
          FlowDesk profesyonel, yoğun ve sakin bir operasyon aracıdır. Kenarlık, tipografi ve
          hizalama; gölge ve dekorasyondan önce gelir. Vurgu rengi seyrek kullanılır.
        </p>
      </header>

      <div className="mt-8 lg:flex lg:gap-10">
        <nav aria-label="Bölümler" className="mb-8 shrink-0 lg:mb-0 lg:w-48">
          <ul className="flex flex-wrap gap-x-4 gap-y-1 lg:sticky lg:top-8 lg:flex-col lg:gap-1">
            {tableOfContents.map((entry) => (
              <li key={entry.id}>
                <a
                  href={`#${entry.id}`}
                  className="text-muted-foreground hover:text-foreground block rounded-md py-1 text-[13px] transition-colors duration-100 lg:px-2"
                >
                  {entry.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>

        <main className="flex min-w-0 flex-1 flex-col gap-10">
          <FoundationsSections />
          <ControlsSections />
          <FeedbackSections />
          <DataSections />
          <NavigationSections />
        </main>
      </div>
    </div>
  );
}
