# FlowDesk — İlerleme

Bu dosya faz seviyesindeki ilerlemeyi takip eder. Her faz sonunda güncellenir.
Operasyonel devir ayrıntısı için `docs/HANDOFF.md`.

**Son güncelleme:** 2026-09-16

---

## Fazlar

- [x] Faz 00 — Bootstrap
- [x] Faz 01 — Temel
- [x] Faz 02 — Tasarım Sistemi
- [ ] Faz 03 — Kimlik Doğrulama
- [ ] Faz 04 — Çok Kiracılılık
- [ ] Faz 05 — Ekip ve Davetler
- [ ] Faz 06 — Müşteriler
- [ ] Faz 07 — Talepler
- [ ] Faz 08 — Görevler
- [ ] Faz 09 — Dashboard
- [ ] Faz 10 — RabbitMQ ve Worker
- [ ] Faz 11 — Outbox
- [ ] Faz 12 — E-posta ve Bildirimler
- [ ] Faz 13 — Dosya Ekleri
- [ ] Faz 14 — Denetim ve Etkinlik
- [ ] Faz 15 — Redis Önbellek
- [ ] Faz 16 — Gelişmiş Entegrasyon Testleri
- [ ] Faz 17 — Playwright E2E
- [ ] Faz 18 — Gözlemlenebilirlik
- [ ] Faz 19 — Güvenlik Sertleştirme
- [ ] Faz 20 — CI/CD
- [ ] Faz 21 — Demo Verisi
- [ ] Faz 22 — README ve Portfolyo Cilası
- [ ] Faz 23 — Nihai Üretim Denetimi

## Aktif faz

**Faz 03 — Kimlik Doğrulama**

Durum: Başlanmadı

---

## Faz geçmişi

### Faz 02 — Tasarım Sistemi · Tamamlandı

Token sistemi:

- `docs/DESIGN_SYSTEM.md` içindeki nötr, petrol ve anlamsal renk ölçekleri CSS
  değişkeni olarak tanımlandı ve `@theme inline` ile Tailwind'e bağlandı.
- Yarıçap ölçeği tek bir tabandan türetildi (`--radius: 6px`): rozet ~4 px,
  buton ve girdi 6 px, kart ~8 px, diyalog ~11 px.
- Yoğunluk ölçüleri token'landı: tablo satırı 44 px, tablo başlığı 36 px,
  üst çubuk 48 px.
- Grafik renkleri talep durumlarına birebir eşlendi; böylece bir pasta dilimi
  ile tablodaki rozet aynı rengi taşır (Faz 09'da kullanılacak).

Bileşenler:

- shadcn CLI ile Base UI tabanlı 18 bileşen depoya kaynak olarak alındı
  (ADR-0020) ve FlowDesk kararlarına göre uyarlandı.
- Elle yazılanlar: `Field` (etiket, girdi, yardımcı metin ve hatayı tek
  erişilebilir birim olarak bağlar), `Pagination`, `StatusBadge` ailesi,
  `PageHeader`, `EmptyState`, `AppSidebar`.
- Domain kodları (`src/types/domain.ts`) ile Türkçe etiketler
  (`src/lib/domain-labels.ts`) ayrıldı. Eşleme `Record<...>` ile kuruldu:
  domain'e yeni bir durum eklendiğinde eksik etiket derleme hatası üretir.
- `src/lib/format.ts`: tr-TR tarih, sayı ve göreli zaman biçimlendirmesi.

Vitrin:

- `/design-system` sayfası 16 bölümle yayında. Her bölüm yalnızca bileşeni
  değil, kararın gerekçesini de gösteriyor.
- Tüm içerik Türkçe. Demo verisi gerçekçi ve kurgusal; lorem ipsum yok.

Varsayılan kurulumdan sapılan noktalar: rozet hap biçiminden küçük yarıçapa
çevrildi, tablo yoğunluğu ayarlandı, `Alert`'e anlamsal ton varyantları
eklendi, `Toaster`'ın `next-themes` bağımlılığı kaldırıldı.

Görsel doğrulama: üretim derlemesi başlatılıp headless tarayıcıyla ekran
görüntüsü alındı ve bölümler tek tek incelendi.

### Faz 01 — Temel · Tamamlandı

Backend:

- .NET 10 solution (`backend/FlowDesk.slnx`) ve yedi proje oluşturuldu.
- `Directory.Build.props`: nullable reference types, `TreatWarningsAsErrors`,
  .NET analyzer'ları (`AnalysisMode=Recommended`), NuGet açık denetimi.
- `Directory.Packages.props`: merkezî paket sürüm yönetimi (ADR-0016).
- Bağımlılık yönü proje referanslarıyla kuruldu ve 12 mimari testle bağlandı.
- `Infrastructure` ASP.NET Core shared framework'üne bağlanmadı; yalnızca
  ihtiyaç duyduğu Extensions soyutlamalarını alıyor.
- `PostgresOptions` başlangıçta doğrulanıyor (`ValidateOnStart`); eksik bağlantı
  dizesi ilk istekte değil, açılışta hata veriyor.
- `PostgresHealthCheck` yazıldı. Üçüncü taraf paket alınmadı: tek bakımlı paket
  eski framework hattını hedefliyor ve kontrol zaten birkaç satır.
- `/health/live` ve `/health/ready` ayrıldı. Liveness hiçbir bağımlılığı
  kontrol etmiyor; bağımlılık hatası yeniden başlatma döngüsü üretmemeli.
- `Worker` gerçek bir host kabuğu olarak bırakıldı; sahte bir arka plan
  döngüsü eklenmedi.

Frontend:

- Next.js 16.3.5 + React 19.2.8 + Tailwind 4.3.3 kuruldu, sürümler sabitlendi.
- TypeScript 6.0.3 katı kip: `noUncheckedIndexedAccess`, `noImplicitOverride`,
  `noImplicitReturns`, `verbatimModuleSyntax`.
- ESLint 9.39.5 flat config; `no-explicit-any` ve `no-unsafe-*` kuralları hata
  seviyesinde, tip farkındalıklı lint açık.
- Prettier + `prettier-plugin-tailwindcss`.
- Şablon içeriği kaldırıldı; IBM Plex Sans/Mono ve `lang="tr"` kuruldu.
  Türkçe karakterler için `latin-ext` alt kümesi eklendi.

Altyapı:

- `infra/docker-compose.yml` yalnızca PostgreSQL 17 içeriyor (ADR-0008).
  Sağlık kontrolü, adlandırılmış hacim ve tr_TR harmanlaması yapılandırıldı.

Testler:

- 12 mimari testi: bağımlılık yönü, katman sızıntısı, altyapı paketi kontrolü.
- 5 entegrasyon testi: Testcontainers ile gerçek PostgreSQL üzerinde sağlık
  uç noktaları. Hem olumlu hem olumsuz durum test ediliyor — veritabanı
  erişilemezken readiness 503, liveness 200 dönüyor.

Bu faz sırasında çözülen iki gerçek hata:

1. xUnit v3, .NET 10 SDK'da VSTest hedefiyle çalışmıyor. Çalıştırıcı seçimi
   kökteki `global.json` içine taşındı (ADR-0018).
2. `.env` içindeki bağlantı dizesi tırnaksızdı; noktalı virgül shell tarafından
   komut ayracı sayılıp dize `Host=localhost` olarak kırpılıyor ve API yanlış
   veritabanına bağlanıyordu. Değer tırnak içine alındı.

### Faz 00 — Bootstrap · Tamamlandı

Yapılanlar:

- Yerel Git deposu başlatıldı, `main` dalı oluşturuldu.
- Depo dizin iskeleti kuruldu: `backend/`, `frontend/`, `e2e/`, `infra/`,
  `docs/`, `.github/workflows/`.
- `.gitignore` yazıldı; gizli bilgi desenleri (`.env*`, anahtar, sertifika)
  ilk commit'ten önce kapsama alındı.
- `.editorconfig` yazıldı; C# stil kuralları, isimlendirme kuralları ve
  analiz uyarı seviyeleri tanımlandı.
- `.env.example` yazıldı; port haritası ve yapılandırma yüzeyi belgelendi.
  Yalnızca örnek değerler içerir.
- `CLAUDE.md` yazıldı; kalıcı çalışma kuralları ve doküman haritası.
- Dokümantasyon yazıldı: `PRODUCT.md`, `ARCHITECTURE.md`, `SECURITY.md`,
  `DATABASE.md`, `API_CONVENTIONS.md`, `DESIGN_SYSTEM.md`, `DECISIONS.md`,
  `ROADMAP.md`, `PROGRESS.md`, `HANDOFF.md`.
- 16 ADR kaydedildi (ADR-0001 … ADR-0016).
- .NET 10.0.401 LTS SDK kuruldu ve doğrulandı.

Ön koşul olarak çözülen ortam sorunu: macOS 26 / Apple Silicon üzerinde
`dotnet-install.sh` mevcut `~/.dotnet` kurulumunun üzerine yazdığında host
ikilisi `SIGKILL (Code Signature Invalid)` alıyordu. Kök neden, çekirdeğin o
inode için tuttuğu bayat imza değerlendirmesiydi. Host ikilisi yeni bir inode
ile değiştirilerek çözüldü. Ayrıntı: ADR-0010.

---

## Bilinen sorunlar

Yok.

## Teknik borç

Yok.

## Ertelenen özellikler

Çekirdek kapsam dışında bırakılanlar ve gerekçeleri `docs/ROADMAP.md`
içindedir: koyu tema, Google OAuth, wildcard subdomain, Kanban, WebSocket,
gerçek zamanlı varlık, faturalandırma, Kubernetes, mikroservisler, mobil
uygulama, özelleştirilebilir iş akışları.

## Doğrulama durumu

Faz 01 sonunda gerçekten çalıştırılan komutlar:

| Komut | Sonuç |
|---|---|
| `dotnet restore backend/FlowDesk.slnx` | Başarılı |
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 17/17 başarılı |
| `npm --prefix frontend run lint` | Başarılı |
| `npm --prefix frontend run typecheck` | Başarılı |
| `npm --prefix frontend run format:check` | Başarılı |
| `npm --prefix frontend run build` | Başarılı |
| `docker compose ... config` | Geçerli |
| `docker compose ... up -d` | `flowdesk-postgres` healthy |
| `GET /health/live` | HTTP 200 |
| `GET /health/ready` | HTTP 200, `postgres = Healthy` |
| `dotnet list package --vulnerable --include-transitive` | Açık yok |
| `npm --prefix frontend audit` | 0 açık |

Faz 02 sonunda:

| Komut | Sonuç |
|---|---|
| `npm --prefix frontend run lint` | Başarılı |
| `npm --prefix frontend run typecheck` | Başarılı |
| `npm --prefix frontend run format:check` | Başarılı |
| `npm --prefix frontend run build` | Başarılı — `/design-system` statik üretildi |
| Görsel inceleme | Tipografi, renk, tablo, kenar çubuğu ve buton bölümleri ekran görüntüsüyle doğrulandı |
