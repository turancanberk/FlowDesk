# FlowDesk — Agent Devir Dosyası

Bu dosya, konuşma geçmişi olmadan projeye devam edilebilmesi için tutulur.
Kısa, güncel ve operasyonel olmalıdır.

> **Bu dosyaya körlemesine güvenme.** Depo gerçekliği her zaman önceliklidir.
> Bu dosya ile kod/Git durumu çelişiyorsa depoyu esas al, farkı araştır,
> bu dosyayı düzelt, sonra devam et.

---

## Son Güncelleme

2026-09-16 (UTC)

## Repository Durumu

| Alan | Değer |
|---|---|
| Aktif dal | `feat/design-system` |
| Son commit | Faz 01 commit'i ile güncellenecek |
| Working tree | Faz 01 commit'i ile temizlenecek |
| Remote | `origin` → https://github.com/turancanberk/FlowDesk (public) |

## Tamamlanan Fazlar

- Faz 00 — Bootstrap
- Faz 01 — Temel
- Faz 02 — Tasarım Sistemi

## Şu Anda Nerede Kaldık?

**Faz 03 — Kimlik Doğrulama** (henüz başlanmadı)

### Tamamlananlar

Faz 03 kapsamında henüz iş yapılmadı.

### Devam Eden İş

Yok.

### Henüz Yapılmayanlar

Faz 03'ün tamamı:

- ASP.NET Core Identity ve `FlowDeskDbContext` (ilk migration burada oluşur)
- `RefreshToken` varlığı: `FamilyId`, `TokenHash`, `UsedAt`, `RevokedAt`
- `POST /api/auth/register`, `login`, `refresh`, `logout`; `GET /api/me`
- Access token üretimi (kısa ömürlü JWT) ve refresh token rotasyonu
- Replay tespiti: kullanılmış bir token tekrar sunulursa ailenin tamamı iptal
- Çerez politikası, CORS ve CSRF savunma katmanları
- Rate limiting: login, register, refresh
- Frontend: bellekte auth store, `Authorization: Bearer` ekleyen HTTP
  istemcisi, sessiz yenileme, tek uçuşlu (single-flight) refresh,
  giriş/kayıt ekranları, korumalı rota guard'ı
- Entegrasyon testleri: rotasyon, replay, iptal, logout, rate limit

## Bir Sonraki Yapılacak İş

`feat/authentication` dalını aç. Backend'de `FlowDesk.Infrastructure` içinde
`FlowDeskDbContext` oluştur ve ASP.NET Core Identity'yi bağla
(`Microsoft.AspNetCore.Identity.EntityFrameworkCore`). `AspNetRoles`
**uygulama rolleri için kullanılmaz**; roller Faz 04'te `Membership` üzerinde
olacak (ADR-0003). Ardından `RefreshToken` varlığını
`docs/DATABASE.md` içindeki şemaya göre ekle, ilk migration'ı üret ve boş
veritabanına uygula.

Kimlik doğrulama modelinin tamamı `docs/SECURITY.md` bölüm 1'de tanımlı ve
bağlayıcıdır: access token yalnızca bellekte, refresh token `HttpOnly` çerezde
ve veritabanında yalnızca SHA-256 hash'i olarak saklanır.

Paket sürümlerini kurulum anında resmî registry'den doğrula (ADR-0016).

## Son Doğrulama Durumu

Faz 01 sonunda gerçekten çalıştırıldı:

| Komut | Sonuç |
|---|---|
| `dotnet restore backend/FlowDesk.slnx` | Başarılı |
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 17/17 başarılı (12 mimari + 5 entegrasyon) |
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

Kiracı izolasyonu bir **güvenlik sınırıdır**. Kullanıcı arayüzü Türkçe, kaynak
kod tanımlayıcıları İngilizce, commit mesajları Türkçe.

## Değiştirilen Önemli Dosyalar

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
| Son migration | Yok |
| Migration uygulandı mı | Hayır — henüz `DbContext` yok (Faz 03'te Identity ile gelir) |
| Seed | Yok (Faz 21) |

PostgreSQL 17.10 konteyneri çalışıyor ve `flowdesk` veritabanı boş.

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
