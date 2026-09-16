# FlowDesk — Agent Devir Dosyası

Bu dosya, konuşma geçmişi olmadan projeye devam edilebilmesi için tutulur.
Kısa, güncel ve operasyonel olmalıdır.

> **Bu dosyaya körlemesine güvenme.** Depo gerçekliği her zaman önceliklidir.
> Bu dosya ile kod/Git durumu çelişiyorsa depoyu esas al, farkı araştır,
> bu dosyayı düzelt, sonra devam et.

---

## Son Güncelleme

2026-09-17 (UTC)

## Repository Durumu

| Alan | Değer |
|---|---|
| Aktif dal | `feat/customers` |
| Son commit | Faz 01 commit'i ile güncellenecek |
| Working tree | Faz 01 commit'i ile temizlenecek |
| Remote | `origin` → https://github.com/turancanberk/FlowDesk (public) |

## Tamamlanan Fazlar

- Faz 00 — Bootstrap
- Faz 01 — Temel
- Faz 02 — Tasarım Sistemi
- Faz 03 — Kimlik Doğrulama
- Faz 04 — Çok Kiracılılık
- Faz 05 — Ekip ve Davetler
- Faz 06 — Müşteriler

## Şu Anda Nerede Kaldık?

**Faz 07 — Talepler** (henüz başlanmadı)

### Tamamlananlar

Faz 07 kapsamında henüz iş yapılmadı.

### Devam Eden İş

Yok.

### Henüz Yapılmayanlar

Faz 07'nin tamamı:

- `Ticket` ve `TicketComment` domain varlıkları
- Kiracı içinde sıralı talep numarası (`TLP-1042`)
- Durum geçişleri: geçersiz geçişler domain seviyesinde reddedilecek
- İyimser eşzamanlılık (`xmin`), çakışmada `409`
- Atama, öncelik, müşteri ilişkisi, yorumlar
- Filtre, arama, sayfalama, detay arayüzü
- Müşteri detayındaki "Talepler" sekmesinin doldurulması

## Bir Sonraki Yapılacak İş

`feat/tickets` dalını aç. `FlowDesk.Domain/Tickets/Ticket.cs` dosyasını
`docs/DATABASE.md` şemasına göre yaz ve `ITenantOwned` uygula.

İki nokta özellikle dikkat ister:

**Talep numarası.** Kiracı başına sıralı olacak ve eşzamanlı oluşturmada
çakışmamalı. `TenantCounters` tablosundaki satır aynı transaction içinde
kilitlenip artırılacak (`docs/DATABASE.md`). `(TenantId, Number)` benzersiz
indeksi son savunma hattı.

**Durum geçişleri.** `Ticket` kendi geçiş kurallarını koruyacak; geçersiz bir
geçiş entity tarafından reddedilecek, yalnızca use case'te kontrol edilmeyecek.
`RefreshToken` ve `Invitation` aynı deseni izliyor, onlara bakılabilir.

İyimser eşzamanlılık yalnızca `Ticket` için (ADR-0013): PostgreSQL `xmin`
sistem sütunu eşzamanlılık belirteci olarak yapılandırılacak, çakışmada `409`
dönülecek.

İzin matrisine talep eylemleri eklenecek; `Every_defined_action_is_mapped`
testi eksik eşlemeyi yakalar.

## Son Doğrulama Durumu

Faz 01 sonunda gerçekten çalıştırıldı:

| Komut | Sonuç |
|---|---|
| `dotnet restore backend/FlowDesk.slnx` | Başarılı |
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 241/241 başarılı |
| `npm --prefix frontend run lint` | Başarılı |
| `npm --prefix frontend run typecheck` | Başarılı |
| `npm --prefix frontend run format:check` | Başarılı |
| `npm --prefix frontend run build` | Başarılı |
| `docker compose --env-file .env -f infra/docker-compose.yml up -d` | `flowdesk-postgres` healthy |
| `npm --prefix frontend run format:check` (Faz 02) | Başarılı |
| `/design-system` görsel inceleme (Faz 02) | Tipografi, renk, tablo, kenar çubuğu ve butonlar doğrulandı |
| `GET http://localhost:5080/health/live` | HTTP 200 |
| `GET http://localhost:5080/health/ready` | HTTP 200, `postgres = Healthy` |
| `dotnet list ... package --vulnerable --include-transitive` | Açık yok |
| `npm --prefix frontend audit` | 0 açık |
| Tarayıcıda uçtan uca akış (Faz 03) | Kayıt → yenileme → çıkış → hatalı giriş → giriş tamam |
| `localStorage` / `sessionStorage` (Faz 03) | Boş — ADR-0006 doğrulandı |
| Çerezler (Faz 03) | Yalnızca `flowdesk_refresh_token`; `HttpOnly`, `SameSite=Strict`, `Path=/api/auth` |
| Tarayıcıda çok kiracılı akış (Faz 04) | Yedi adımın tamamı geçti; izolasyon doğrulandı |
| Tarayıcıda davet akışı (Faz 05) | Dokuz adımın tamamı geçti; yanlış hesap ve ikinci kullanım reddedildi |
| Tarayıcıda müşteri akışı (Faz 06) | On adımın tamamı geçti; Türkçe arama ve arşivleme doğrulandı |

## Mevcut Hatalar / Blokerler

Bilinen blocker yok.

Çözülmüş, tekrarlayabilecek ortam sorunları:

1. **macOS 26 / Apple Silicon, .NET kurulumu.** `dotnet-install.sh` mevcut bir
   `~/.dotnet` kurulumunun üzerine yazdığında host ikilisi
   `SIGKILL (Code Signature Invalid)` alır. Crash raporunda sonlanma nedeni
   `CODESIGNING / Taskgated Invalid Signature` görünür; `codesign -v` imzayı
   geçerli bulur. Sorun çekirdeğin o inode için tuttuğu bayat değerlendirmedir.
   Çözüm:
   ```bash
   cd ~/.dotnet && cp dotnet dotnet.new && mv -f dotnet.new dotnet && chmod +x dotnet
   ```

2. **`.env` içinde tırnaksız bağlantı dizesi.** Noktalı virgül shell tarafından
   komut ayracı sayılır; `set -a; . ./.env` ile okunduğunda dize
   `Host=localhost` olarak kırpılır ve API yanlış veritabanına bağlanır.
   `.env.example` içindeki değer tırnaklıdır; bu tırnaklar kaldırılmamalıdır.

3. **Docker Compose ve `.env` konumu.** Compose, `.env` dosyasını compose
   dosyasının dizinine göre arar. Depo kökündeki `.env` için komutlarda
   `--env-file .env` verilmelidir.

4. **Bulut senkronizasyonu çakışma kopyaları.** Depo iCloud Drive ile
   senkronize edilen bir dizinde (`Desktop`). Senkronizasyon zaman zaman
   `Program 2.cs` gibi kopyalar üretiyor ve `dotnet run` "birden fazla proje
   dosyası" hatası veriyor. Desen `.gitignore`'a eklendi; hata görülürse:
   ```bash
   find . -name "* 2.*" -not -path "*/node_modules/*" -not -path "*/bin/*" \
     -not -path "*/obj/*" -not -path "./.git/*" -not -path "*/.next/*" -delete
   ```

5. **Rate limiting ve elle doğrulama.** Kayıt limiti IP başına 10 dakikada 5.
   Tarayıcıda arka arkaya doğrulama yaparken bu limit dolar ve kayıt `429`
   döner. Limiter bellek içidir; API'yi yeniden başlatmak sayaçları sıfırlar.

6. **Playwright ve `networkidle`.** TanStack Query açık istekler tutabildiği
   için `waitUntil: "networkidle"` hiç sonuçlanmayabilir. Elle doğrulama
   betiklerinde `domcontentloaded` + açık seçici beklemesi kullanın.

## Önemli Mimari Kararlar

Ayrıntı `docs/DECISIONS.md` içindedir; burada yalnızca hatırlatma:

- Modüler monolit, mikroservis yok (ADR-0001)
- Paylaşılan veritabanı + paylaşılan şema çok kiracılılık (ADR-0002)
- User / Tenant / Membership; rol `Membership` üzerinde (ADR-0003)
- MediatR yok, generic repository yok, generic UnitOfWork yok (ADR-0004)
- Yol tabanlı workspace rotalama (ADR-0005)
- Access token yalnızca bellekte; refresh token `HttpOnly` çerezde, döner,
  hash'li saklanır, replay tespiti var (ADR-0006)
- Yabancı kiracı kaynağı için `404` (ADR-0007)
- Kademeli altyapı: bileşen, onu kullanan faz gelmeden Compose'a eklenmez
  (ADR-0008)
- Kimlikli veri çekme istemci tarafında; Server Component'lar access token'a
  erişemez (ADR-0009)
- .NET 10 LTS (ADR-0010), TypeScript 6 hattı (ADR-0011), ESLint 9 hattı
  (ADR-0017)
- Soft delete yalnızca `Customer` (ADR-0012)
- İyimser eşzamanlılık yalnızca `Ticket` (ADR-0013)
- Outbox + at-least-once idempotency (ADR-0014)
- Redis yalnızca dashboard önbelleği (ADR-0015)
- Merkezî paket yönetimi, kurulum anında sürüm doğrulama (ADR-0016)
- xUnit v3 + Microsoft.Testing.Platform; çalıştırıcı kökteki `global.json`
  içinde (ADR-0018)
- Çözüm dosyası `.slnx` (ADR-0019)
- shadcn/ui bileşenleri Base UI tabanlı olarak depoda sahiplenilir; shadcn'in
  semantik değişken sözleşmesi korunur, değerleri FlowDesk paletiyle
  doldurulur (ADR-0020)
- Application EF Core kullanabilir ama veritabanı sağlayıcısını kullanamaz;
  sınır `IFlowDeskDbContext` ve mimari testle korunur (ADR-0021)
- Kullanıcı hesabı Identity'ye aittir, Domain'de `User` varlığı yoktur;
  domain varlıkları kullanıcıya `Guid` ile referans verir (ADR-0022)
- API'de enum'lar **ada göre** serileştirilir; .NET istemcileri
  `FlowDeskJson.Options` kullanmalıdır (ADR-0023)
- `ITenantOwned` işaretleyicisi global query filter'a otomatik kayıt sağlar;
  `Membership` bilinçli istisnadır (ADR-0024)
- Türkçe arama katlanmış `SearchIndex` sütunu üzerinden yapılır; SQL `LOWER()`
  noktalı/noktasız i'yi doğru katlayamaz (ADR-0025)
- TanStack Table **v8** kullanılır, v9 değil (ADR-0026)

Kiracı izolasyonu bir **güvenlik sınırıdır**. Kullanıcı arayüzü Türkçe, kaynak
kod tanımlayıcıları İngilizce, commit mesajları Türkçe.

## Değiştirilen Önemli Dosyalar

Faz 06'da eklenenler:

- `backend/src/FlowDesk.Domain/Customers/` — `Customer`, `CustomerStatus`
- `backend/src/FlowDesk.Domain/Common/TurkishText.cs` — ortak harf katlaması
- `backend/src/FlowDesk.Application/Customers/` — altı use case
- `backend/src/FlowDesk.Application/Common/PagedResult.cs`
- `backend/src/FlowDesk.Api/Endpoints/CustomerEndpoints.cs`
- `backend/tests/.../Customers/` — izolasyon ve liste testleri
- `frontend/src/features/customers/` — liste, detay, form
- `scripts/clean-sync-duplicates.sh`

Faz 05'te eklenenler:

- `backend/src/FlowDesk.Domain/Tenancy/Invitation.cs`
- `backend/src/FlowDesk.Application/Team/` — yedi use case + `TeamErrors`
- `backend/src/FlowDesk.Api/Endpoints/TeamEndpoints.cs`
- `backend/tests/.../Team/` — davet güvenliği ve rol yetkilendirme testleri
- `frontend/src/features/team/` — ekip ekranı, davet diyaloğu, kabul ekranı
- `frontend/src/features/auth/safe-redirect.ts` — açık yönlendirme koruması

Faz 04'te eklenenler:

- `backend/src/FlowDesk.Domain/Tenancy/` — `Tenant`, `Membership`,
  `WorkspaceSlug`, `MembershipRole`, `ITenantOwned`
- `backend/src/FlowDesk.Application/Tenancy/` — izin matrisi ve beş use case
- `backend/src/FlowDesk.Api/Tenancy/` — `WorkspaceResolutionFilter`,
  `TenantContext`, `CurrentUser`
- `backend/src/FlowDesk.Infrastructure/Persistence/FlowDeskDbContext.cs` —
  global query filter mekanizması
- `backend/tests/.../Tenancy/` — izolasyon, query filter ve sözleşme testleri
- `frontend/src/features/workspaces/` — liste, oluşturma, kabuk, seçici
- `frontend/src/app/error.tsx`, `not-found.tsx` — Türkçe hata ekranları

Faz 03'te eklenenler:

- `backend/src/FlowDesk.Domain/Authentication/RefreshToken.cs`
- `backend/src/FlowDesk.Application/Common/` — `Result`, `ApplicationError`
- `backend/src/FlowDesk.Application/Abstractions/` — beş sözleşme
- `backend/src/FlowDesk.Application/Authentication/` — beş use case +
  `SessionIssuer`
- `backend/src/FlowDesk.Infrastructure/Identity/`, `Authentication/`,
  `Persistence/` — `FlowDeskDbContext`, ilk migration
- `backend/src/FlowDesk.Api/Endpoints/AuthEndpoints.cs`,
  `Authentication/RefreshTokenCookie.cs`, `Common/` (Result eşlemesi,
  doğrulama filtresi, rate limiting, çerez politikası koruması)
- `frontend/src/lib/api/` — bellekte token deposu, HTTP istemcisi
- `frontend/src/features/auth/` — API, sorgular, formlar, rota koruması
- `frontend/src/app/(auth)/giris`, `(auth)/kayit`

Faz 02'de eklenenler:

- `frontend/src/app/globals.css` — tüm tasarım token'ları burada
- `frontend/src/components/ui/` — 18 shadcn/Base UI bileşeni + elle yazılan
  `field.tsx` ve `pagination.tsx`
- `frontend/src/components/product/` — `status-badge.tsx`, `page-header.tsx`,
  `empty-state.tsx`, `app-sidebar.tsx`
- `frontend/src/types/domain.ts` ve `frontend/src/lib/domain-labels.ts` —
  domain kodu / Türkçe etiket sınırı
- `frontend/src/lib/format.ts` — tr-TR biçimlendirme
- `frontend/src/app/design-system/` — vitrin sayfası ve bölümleri

Faz 01'de eklenenler:

- `global.json` — SDK sürümü ve `dotnet test` çalıştırıcısı (depo kökünde)
- `backend/Directory.Build.props`, `backend/Directory.Packages.props`
- `backend/FlowDesk.slnx` ve yedi proje
- `backend/src/FlowDesk.Infrastructure/Persistence/PostgresOptions.cs`
- `backend/src/FlowDesk.Infrastructure/HealthChecks/PostgresHealthCheck.cs`
- `backend/src/FlowDesk.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- `backend/src/FlowDesk.Api/Program.cs`, `Endpoints/HealthEndpoints.cs`
- `backend/tests/FlowDesk.UnitTests/Architecture/` — bağımlılık yönü testleri
- `backend/tests/FlowDesk.IntegrationTests/` — Testcontainers desteği ve
  sağlık uç noktası testleri
- `infra/docker-compose.yml`
- `frontend/` — Next.js uygulaması, ESLint, Prettier, tsconfig

## Database Durumu

| Alan | Durum |
|---|---|
| Son migration | `AddCustomers` |
| Migration uygulandı mı | Evet — yerel `flowdesk` veritabanına uygulandı |
| Tablolar | Identity kullanıcı tabloları, `RefreshTokens`, `Tenants`, `Memberships`, `Invitations`, `Customers` |
| Seed | Yok (Faz 21) |

Identity rol tabloları bilinçli olarak oluşturulmadı (ADR-0022).

Entegrasyon testleri kendi Testcontainers örneğini kullanır ve migration'ları
fixture içinde uygular; yerel veritabanına dokunmaz.

## Infrastructure Durumu

| Servis | Durum |
|---|---|
| PostgreSQL | **Aktif** — `flowdesk-postgres`, host portu 5433 |
| RabbitMQ | Henüz projeye eklenmedi (Faz 10) |
| Mailpit | Henüz projeye eklenmedi (Faz 12) |
| Azurite | Henüz projeye eklenmedi (Faz 13) |
| Redis | Henüz projeye eklenmedi (Faz 15) |
| Prometheus / Grafana | Henüz projeye eklenmedi (Faz 18) |

Bu makinede host portları `5432`, `6379` ve `5000` başka süreçlerce kullanılıyor.
Tam port haritası `docs/ARCHITECTURE.md` içindedir.

## Harici İşlemler

Kullanıcıdan beklenen harici işlem yok.

---

## Sonraki Agent İçin Başlangıç Talimatı

1. `CLAUDE.md` oku.
2. `docs/PROGRESS.md` oku — hangi faz bitti, hangisi aktif.
3. Bu dosyayı oku.
4. `docs/DECISIONS.md` içinde ilgili ADR'leri oku.
5. `git status` çalıştır.
6. `git log --oneline -10` ile son commit'leri incele.
7. Ortamı hazırla ve doğrula:
   ```bash
   cp .env.example .env          # yoksa
   docker compose --env-file .env -f infra/docker-compose.yml up -d
   dotnet test backend/FlowDesk.slnx
   npm --prefix frontend ci
   npm --prefix frontend run lint && npm --prefix frontend run typecheck
   ```
8. "Bir Sonraki Yapılacak İş" bölümünden devam et.
9. Önceki tamamlanmış işi sebepsiz yere yeniden yazma.

### Bu dosyanın güncellenme zamanları

Her faz tamamlandığında; önemli bir alt özellik bittiğinde; yeni migration
sonrası; önemli bir mimari karar sonrası; bir blocker ortaya çıktığında; uzun
bir implementasyona başlamadan önce durum değiştiyse; oturumun güvenli şekilde
kesilebileceği anlamlı checkpoint'lerde.

Gereksiz sık güncelleme commit'i oluşturulmaz.

### Faz ortasında durmak gerekirse

1. Kodu derlenebilir güvenli bir noktaya getir.
2. İlgili testleri çalıştır.
3. Yarım implementasyonu gizleme.
4. Hangi kısmın tamamlanmadığını bu dosyaya açıkça yaz.
5. `docs/PROGRESS.md` içinde fazı tamamlanmış olarak **işaretleme**.
6. Gerçek bir checkpoint commit'i oluştur (bozuk kod commit etme).
7. Remote'a push et.
8. "Bir Sonraki Yapılacak İş" bölümünü somut biçimde güncelle.
