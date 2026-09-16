/*
  Faz 01 geçici karşılama ekranı.

  Tasarım sistemi Faz 02'de kurulur, kimlik doğrulama Faz 03'te gelir. Burada
  bilinçli olarak sahte bir pano veya doldurulmuş bir arayüz gösterilmiyor:
  henüz var olmayan bir şeyin görüntüsünü üretmek ilerleme izlenimi verir ama
  ilerleme değildir.
*/
export default function HomePage() {
  return (
    <main className="mx-auto flex w-full max-w-xl flex-1 flex-col justify-center px-4 py-16">
      <p className="font-mono text-xs tracking-widest text-[var(--color-text-secondary)] uppercase">
        FlowDesk
      </p>

      <h1 className="mt-3 text-xl font-semibold text-[var(--color-text-primary)]">
        Müşteri operasyonları platformu
      </h1>

      <p className="mt-2 text-sm text-[var(--color-text-secondary)]">
        Ekipler müşterilerini, destek taleplerini ve görevlerini tek bir çalışma alanında yönetir.
      </p>

      <div className="mt-8 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] p-4">
        <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">Kurulum durumu</h2>
        <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
          Proje temeli hazır. Sıradaki adım tasarım sistemi; ardından kimlik doğrulama ve çalışma
          alanı altyapısı gelir.
        </p>
      </div>
    </main>
  );
}
