import { ShowcaseSection, Specimen, Surface } from "../_components/showcase-section";

/*
  Temeller: tipografi, renk ve ölçü token'ları.
  Değerler docs/DESIGN_SYSTEM.md ile birebir aynıdır.
*/

const neutralScale = [
  { token: "neutral-0", value: "#ffffff", usage: "Ana yüzey" },
  { token: "neutral-25", value: "#fcfcfd", usage: "Alternatif satır" },
  { token: "neutral-50", value: "#f7f8f9", usage: "Uygulama zemini, tablo başlığı" },
  { token: "neutral-100", value: "#f1f2f4", usage: "Hover yüzeyi" },
  { token: "neutral-200", value: "#e4e6ea", usage: "Kenarlık" },
  { token: "neutral-300", value: "#d2d6dc", usage: "Güçlü kenarlık, girdi" },
  { token: "neutral-400", value: "#a9b0ba", usage: "Devre dışı metin" },
  { token: "neutral-500", value: "#7c8593", usage: "İkincil ikon" },
  { token: "neutral-600", value: "#5c6675", usage: "İkincil metin" },
  { token: "neutral-700", value: "#434c59", usage: "Gövde metni" },
  { token: "neutral-800", value: "#2d343e", usage: "Başlık" },
  { token: "neutral-900", value: "#1a1f26", usage: "Birincil metin" },
];

const petrolScale = [
  { token: "petrol-50", value: "#ecf5f5", usage: "Seçili satır" },
  { token: "petrol-100", value: "#d3e8e8", usage: "Rozet zemini" },
  { token: "petrol-200", value: "#a8d1d2", usage: "Vurgulu kenarlık" },
  { token: "petrol-400", value: "#45959a", usage: "Odak halkası" },
  { token: "petrol-500", value: "#1f7a80", usage: "Birincil eylem" },
  { token: "petrol-600", value: "#16636a", usage: "Birincil eylem hover" },
  { token: "petrol-700", value: "#125156", usage: "Birincil eylem active" },
  { token: "petrol-800", value: "#103f43", usage: "Vurgulu metin" },
];

const semanticColors = [
  { name: "Başarı", text: "#1b6b45", surface: "#e8f5ee", border: "#a9d9c1" },
  { name: "Uyarı", text: "#8a5a00", surface: "#fdf3e2", border: "#edd09a" },
  { name: "Hata", text: "#a32a2a", surface: "#fcecec", border: "#eeb4b4" },
  { name: "Bilgi", text: "#1f5aa8", surface: "#eaf1fb", border: "#b3cbeb" },
];

const typeScale = [
  {
    role: "Sayfa başlığı",
    className: "text-xl font-semibold text-foreground",
    size: "20 / 28 · 600",
  },
  {
    role: "Bölüm başlığı",
    className: "text-base font-semibold text-foreground",
    size: "16 / 24 · 600",
  },
  { role: "Alt başlık", className: "text-sm font-semibold text-foreground", size: "14 / 20 · 600" },
  { role: "Gövde", className: "text-sm text-foreground", size: "14 / 20 · 400" },
  { role: "İkincil", className: "text-[13px] text-muted-foreground", size: "13 / 18 · 400" },
  { role: "Etiket", className: "text-xs font-medium text-muted-foreground", size: "12 / 16 · 500" },
  { role: "Yardımcı metin", className: "text-xs text-muted-foreground", size: "12 / 16 · 400" },
];

function ColorSwatch({ value, token, usage }: { value: string; token: string; usage: string }) {
  return (
    <div className="flex items-center gap-3">
      <div
        className="border-border size-8 shrink-0 rounded-md border"
        style={{ backgroundColor: value }}
        aria-hidden="true"
      />
      <div className="min-w-0">
        <p className="text-foreground font-mono text-xs">{token}</p>
        <p className="text-muted-foreground truncate text-xs">
          <span className="font-mono">{value}</span> · {usage}
        </p>
      </div>
    </div>
  );
}

function FoundationsSections() {
  return (
    <>
      <ShowcaseSection
        id="tipografi"
        title="Tipografi"
        description="IBM Plex Sans arayüzün tamamında kullanılır. IBM Plex Mono yalnızca talep numarası ve kayıt kimliği gibi teknik alanlar içindir. Hiyerarşi punto büyüterek değil, ağırlık ve renkle kurulur."
      >
        <Surface>
          <dl className="divide-border divide-y">
            {typeScale.map((entry) => (
              <div
                key={entry.role}
                className="grid grid-cols-[10rem_1fr_7rem] items-center gap-4 py-2.5"
              >
                <dt className="text-muted-foreground text-xs">{entry.role}</dt>
                <dd className={entry.className}>Talep akışı bu ekrandan yönetilir</dd>
                <dd className="text-muted-foreground text-right font-mono text-[11px]">
                  {entry.size}
                </dd>
              </div>
            ))}
            <div className="grid grid-cols-[10rem_1fr_7rem] items-center gap-4 py-2.5">
              <dt className="text-muted-foreground text-xs">Mono</dt>
              <dd className="text-muted-foreground font-mono text-xs">TLP-1042 · 8f3c1e9a</dd>
              <dd className="text-muted-foreground text-right font-mono text-[11px]">
                12 / 16 · 400
              </dd>
            </div>
          </dl>
        </Surface>
      </ShowcaseSection>

      <ShowcaseSection
        id="renk"
        title="Renk"
        description="Renk anlam taşır, dekorasyon için kullanılmaz. Vurgu rengi seyrek kullanılır: her yerde vurgu varsa hiçbir yerde vurgu yoktur."
      >
        <div className="grid gap-4 lg:grid-cols-2">
          <Surface>
            <Specimen label="Nötr ölçek">
              <div className="mt-1 grid gap-2.5 sm:grid-cols-2">
                {neutralScale.map((color) => (
                  <ColorSwatch key={color.token} {...color} />
                ))}
              </div>
            </Specimen>
          </Surface>

          <div className="flex flex-col gap-4">
            <Surface>
              <Specimen label="Vurgu — petrol">
                <div className="mt-1 grid gap-2.5 sm:grid-cols-2">
                  {petrolScale.map((color) => (
                    <ColorSwatch key={color.token} {...color} />
                  ))}
                </div>
              </Specimen>
            </Surface>

            <Surface>
              <Specimen label="Anlamsal renkler">
                <div className="mt-1 flex flex-col gap-2">
                  {semanticColors.map((color) => (
                    <div
                      key={color.name}
                      className="flex items-center justify-between rounded-md border px-2.5 py-1.5"
                      style={{
                        backgroundColor: color.surface,
                        borderColor: color.border,
                        color: color.text,
                      }}
                    >
                      <span className="text-xs font-medium">{color.name}</span>
                      <span className="font-mono text-[11px] opacity-80">{color.text}</span>
                    </div>
                  ))}
                </div>
              </Specimen>
            </Surface>
          </div>
        </div>
      </ShowcaseSection>

      <ShowcaseSection
        id="olculer"
        title="Ölçüler"
        description="Yarıçap tek bir tabandan türer; 6 px buton ve girdi, 8 px kart, 11 px diyalog. Yükseklikler iş yazılımı yoğunluğunu hedefler."
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <Surface>
            <Specimen label="Köşe yarıçapı">
              <div className="mt-1 flex flex-wrap items-end gap-4">
                {[
                  { name: "rounded-sm", hint: "rozet · ~4px", cls: "rounded-sm" },
                  { name: "rounded-lg", hint: "buton, girdi · 6px", cls: "rounded-lg" },
                  { name: "rounded-xl", hint: "kart · ~8px", cls: "rounded-xl" },
                  { name: "rounded-2xl", hint: "diyalog · ~11px", cls: "rounded-2xl" },
                ].map((radius) => (
                  <div key={radius.name} className="flex flex-col items-center gap-1.5">
                    <div className={`border-border-strong bg-muted size-12 border ${radius.cls}`} />
                    <span className="text-muted-foreground font-mono text-[11px]">
                      {radius.name}
                    </span>
                    <span className="text-muted-foreground text-[11px]">{radius.hint}</span>
                  </div>
                ))}
              </div>
            </Specimen>
          </Surface>

          <Surface>
            <Specimen label="Yükseklikler">
              <div className="mt-1 flex flex-col gap-2">
                {[
                  { name: "Buton / girdi (küçük)", height: "h-7", value: "28 px" },
                  { name: "Buton / girdi (varsayılan)", height: "h-8", value: "32 px" },
                  { name: "Buton / girdi (büyük)", height: "h-9", value: "36 px" },
                  { name: "Tablo başlığı", height: "h-table-head", value: "36 px" },
                  { name: "Tablo satırı", height: "h-row", value: "44 px" },
                  { name: "Üst çubuk", height: "h-topbar", value: "48 px" },
                ].map((entry) => (
                  <div key={entry.name} className="flex items-center gap-3">
                    <div
                      className={`${entry.height} border-border-strong bg-muted w-16 shrink-0 rounded-md border`}
                    />
                    <span className="text-foreground text-xs">{entry.name}</span>
                    <span className="text-muted-foreground ml-auto font-mono text-[11px]">
                      {entry.value}
                    </span>
                  </div>
                ))}
              </div>
            </Specimen>
          </Surface>
        </div>
      </ShowcaseSection>
    </>
  );
}

export { FoundationsSections };
