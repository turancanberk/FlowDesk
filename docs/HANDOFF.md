# FlowDesk — Agent Devir Dosyası

Bu dosya, konuşma geçmişi olmadan projeye devam edilebilmesi için tutulur.
Kısa, güncel ve operasyonel olmalıdır.

> **Bu dosyaya körlemesine güvenme.** Depo gerçekliği her zaman önceliklidir.
> Bu dosya ile kod/Git durumu çelişiyorsa depoyu esas al, farkı araştır,
> bu dosyayı düzelt, sonra devam et.

---

## Son Güncelleme

2026-09-19 (UTC)

## Repository Durumu

| Alan | Değer |
|---|---|
| Aktif dal | `main` (Faz 17 birleştirildi) |
| Son commit | `Merge branch 'feat/e2e'` (Faz 17) — hash birleştirme sonrası yazılır |
| Working tree | Temiz |
| Remote | `origin` → https://github.com/turancanberk/FlowDesk (public) |

## Tamamlanan Fazlar

- Faz 00 — Bootstrap
- Faz 01 — Temel
- Faz 02 — Tasarım Sistemi
- Faz 03 — Kimlik Doğrulama
- Faz 04 — Çok Kiracılılık
- Faz 05 — Ekip ve Davetler
- Faz 06 — Müşteriler
- Faz 07 — Talepler
- Faz 08 — Görevler
- Faz 09 — Dashboard
- Faz 10 — RabbitMQ ve Worker
- Faz 11 — Outbox
- Faz 12 — E-posta ve Bildirimler
- Faz 13 — Dosya Ekleri
- Faz 14 — Denetim ve Etkinlik
- Faz 15 — Redis Önbellek
- Faz 16 — Gelişmiş Entegrasyon Testleri
- Faz 17 — Playwright E2E

## Şu Anda Nerede Kaldık?

**Faz 18 — Gözlemlenebilirlik** (henüz başlanmadı)

### Tamamlananlar

Faz 18 kapsamında henüz iş yapılmadı. Faz 17'nin özeti `docs/PROGRESS.md`
içinde; kararı ADR-0041, sekmeler arası yenileme notu ADR-0038'de.

### Devam Eden İş

Yok.

### Henüz Yapılmayanlar

Faz 18'in tamamı (ROADMAP): Serilog, OpenTelemetry, Prometheus, Grafana.

## Bir Sonraki Yapılacak İş

`feat/observability` dalını aç. Prometheus ve Grafana bu fazda Compose'a
girer (ADR-0008); portlar ARCHITECTURE.md'de ayrılmış (9090, 3001).

Başlanacak yerler:

- **Yapılandırılmış log.** API ve Worker aynı biçimde loglamalı; bir isteğin
  outbox → broker → tüketici zinciri tek bir korelasyon kimliğiyle
  izlenebilmeli. Faz 15–16 arasında Worker'ın her outbox turu hata verdi ve
  bunu kimse görmedi — bu fazın somut hedefi o hatanın görünür olması.
- **Metrikler.** İstek süreleri, outbox'ta bekleyen ve yeniden denenen mesaj
  sayısı, tüketici hataları, önbellek isabet oranı.
- **Log hijyeni** (SECURITY.md §12): token, parola ve davet token'ı loglara
  girmemeli; bunu doğrulayan bir test yaz.

Dikkat edilecekler:

- Sürümleri kurulumda registry'den doğrula; yalnızca stable.
- Faz sonu komutlarına artık E2E de dahil (CLAUDE.md). E2E sırasında açık bir
  geliştirme Worker'ı varsa kapat: aynı kuyrukları dinler.

## Son Doğrulama Durumu

Fazların sonunda gerçekten çalıştırılanlar (en günceli en altta):

| Komut | Sonuç |
|---|---|
| `dotnet restore backend/FlowDesk.slnx` | Başarılı |
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 478/478 başarılı (225 birim + 253 entegrasyon) |
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
| Uçtan uca talep akışı (Faz 07) | Çalışan API'ye karşı 14 adımın tamamı geçti — numaralandırma, geçersiz geçiş `409`, eşzamanlılık `409`, izolasyon `404`, rol matrisi |
| `/app/{slug}/tickets` ve `/tickets/{id}` render (Faz 07) | HTTP 200, doğru başlıklar, uygulama hatası yok |
| `AddTickets` migration SQL'i (Faz 07) | `xmin` sistem sütunu `CREATE TABLE` çıktısında yok — doğru |
| Uçtan uca görev akışı (Faz 08) | Çalışan API'ye karşı 14 adımın tamamı geçti — gecikme hesabı, tamamlanma tarihinin temizlenmesi, null'un alanı temizlemesi, sıralama, izolasyon `404`, rol matrisi |
| `/app/{slug}/tasks` render (Faz 08) | HTTP 200, doğru başlık, uygulama hatası yok |
| Uçtan uca dashboard akışı (Faz 09) | Çalışan API'ye karşı 11 adımın tamamı geçti — açık talep tanımı, atanmamış sayımı, geciken/bu hafta ayrımı, enum sıralı dağılım, izolasyon `404`, izleyici erişimi |
| `/app/{slug}/dashboard` render (Faz 09) | HTTP 200, doğru başlık, uygulama hatası yok |
| `docker compose ... up -d` (Faz 10) | `flowdesk-postgres` ve `flowdesk-rabbitmq` healthy |
| `GET /health/ready` (Faz 10, broker ayakta) | 200, `postgres: Healthy`, `rabbitmq: Healthy` |
| `GET /health/ready` (Faz 10, broker erişilemez) | 200, genel `Degraded`, `rabbitmq: Degraded` |
| `dotnet run --project backend/src/FlowDesk.Worker` (Faz 10) | Başladı; "hiçbir kuyruk dinlenmiyor" logladı |
| `dotnet run --project backend/src/FlowDesk.Worker` (Faz 11) | Outbox işleyici başladı ve `FOR UPDATE SKIP LOCKED` sorgusunu gerçekten çalıştırdı |
| Uçtan uca talep akışı (Faz 11) | Tekrar koşuldu; yayınlayıcı değişimi ürün akışını bozmadı |
| `docker compose ... up -d` (Faz 12) | `postgres`, `rabbitmq` ve `mailpit` healthy |
| Worker (Faz 12) | Üç kuyruğu da dinledi; outbox işleyici çalıştı |
| Mailpit üzerinden 8 adımlık akış (Faz 12) | Tamamı geçti — davet e-postası token'ı taşıdı, atama e-postası ve bildirimi ulaştı, kendine atama bildirim üretmedi, yorum bildirimi geldi ve e-posta gitmedi, okundu işaretleme çalıştı, başkasının bildirim kimliği hiçbir şeyi değiştirmedi |
| 8 adımlık önbellek akışı (Faz 15) | Tamamı geçti — anahtarın çalışma alanı kimliğini taşıması, önbellek isabeti, TTL, yazma sonrası silinme, alanlar arası sızmama |
| 10 adımlık etkinlik akışı (Faz 14) | Tamamı geçti — sıra, davet kaydında token olmaması, katılımın doğru alana yazılması, silinen talebin kaydının kalması, izolasyon, rol değişimi, izleyici erişimi |
| 13 adımlık dosya akışı (Faz 13) | Tamamı geçti — bayt bayt indirme, SVG/HTML/boş/limit üstü reddi, yol taşıyan adın temizlenmesi, izolasyon `404`, başka talebin altından erişilememesi, rol matrisi, silme, talep silinince dosyaların gitmesi |
| `dotnet test backend/FlowDesk.slnx` (Faz 16) | 501/501 başarılı (230 birim + 271 entegrasyon) |
| `npm --prefix frontend run format:check / lint / typecheck / build` (Faz 16) | Başarılı |
| Gerçek Worker, Development (Faz 16) | Üç kuyruk dinlendi, outbox turları hatasız (düzeltmeden önce başlamıyordu) |
| `dotnet test backend/FlowDesk.slnx` (Faz 17) | 505/505 başarılı (230 birim + 275 entegrasyon) |
| `npm --prefix e2e test` (Faz 17) | 12/12 başarılı; art arda koşularda kararlı |
| `npm --prefix frontend run format:check / lint / typecheck / build` (Faz 17) | Başarılı |

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

6. **Kararsız test çıktısını saklama alışkanlığı.** Faz 08'de tam çözüm
   koşusunda ara sıra tek bir test düşüyordu ve ilk seferinde çıktı `tail` ile
   kırpıldığı için hangi test olduğu kaybedildi. İkinci düşüşte çıktı
   saklanınca sebep hemen görüldü ve düzeltildi (aşağıda). Kararsız bir düşüş
   görürseniz koşuyu çıktıyı saklayarak tekrarlayın:
   ```bash
   dotnet test backend/FlowDesk.slnx 2>&1 | tee /tmp/flowdesk-test.log
   ```

7. **Docker VM'in durması.** Bir kez tüm projelerin konteynerleri aynı anda
   çıktı (RabbitMQ 137, diğerleri 0) ve Testcontainers tabanlı testler
   başlayamadı. Belirtisi, konteyner başlatma hatası veren ve kodla ilgisi
   olmayan toplu başarısızlık. Çözüm:
   ```bash
   docker compose -f infra/docker-compose.yml --env-file .env up -d
   ```

8. **Playwright ve `networkidle`.** TanStack Query açık istekler tutabildiği
   için `waitUntil: "networkidle"` hiç sonuçlanmayabilir. Elle doğrulama
   betiklerinde `domcontentloaded` + açık seçici beklemesi kullanın.

9. **Docker Desktop kapalı.** Oturum başında daemon çalışmıyorsa Compose ve
   Testcontainers "Cannot connect to the Docker daemon" verir. `open -a Docker`
   ile başlatıp `docker info` yanıt verene kadar bekleyin.

10. **`dotnet test` filtreleri.** `--filter-class` ile `--filter-namespace`
    birlikte verilince VE ile birleşir ve hiç test seçilmeyebilir ("0 test").
    Aynı türden birden fazla filtre VEYA ile birleşir.

11. **Yarış testleri.** Eşzamanlılık testleri istekleri yalnızca aynı anda
    göndermez; `TableGate` ile handler'ın okuma ve yazma arasında dokunduğu
    tabloyu kilitler. Gerçek zamanlamaya bırakılan bir yarış çoğu zaman hiç
    oluşmaz ve test boşuna geçer. Yeni bir yarış testi yazarken önce
    düzeltmesiz kodda düştüğünü görün.

12. **iCloud ve `next build`.** Senkronizasyon, derleme sırasında `.next`
    içinde "… 2" kopyaları üretiyor ve `next build` `ENOTEMPTY` ile düşüyor.
    Bu makinede `frontend/.next`, senkronize edilmeyen `.next.nosync`
    klasörüne bir bağ; `.gitignore`, ESLint ve Prettier ikisini de dışarıda
    tutuyor. Temiz bir kopyada gerekirse:
    ```bash
    cd frontend && rm -rf .next && mkdir .next.nosync && ln -s .next.nosync .next
    ```
    `scripts/clean-sync-duplicates.sh` artık uzantısız dosyaları ve boş kopya
    klasörleri de temizliyor.

13. **E2E çalıştırma.** Compose ayakta olmalı; suite API'yi (5180), Worker'ı
    ve frontend'in üretim derlemesini (3100) kendisi başlatır. İlk kurulum:
    ```bash
    npm --prefix e2e ci && npx --prefix e2e playwright install chromium
    npm --prefix e2e test
    ```
    Suite frontend'i `NEXT_PUBLIC_API_BASE_URL=http://localhost:5180` ile
    derler; ardından `npm --prefix frontend run build` çalıştırılırsa
    varsayılan derleme geri gelir. Başarısız bir senaryonun izi
    `e2e/test-results/` içindedir (`npx playwright show-trace …`).

14. **Playwright seçicileri.** Seçiciler rol ve erişilebilir addan yazılır.
    Bir ekranın gerçek ağacını görmek için `locator.ariaSnapshot()` dökümü en
    hızlı yol; modal açıkken sayfanın geri kalanı ağaçtan gizlenir.

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
- Talepte durum ve atama, `PATCH` alanı değil kendi eylem rotalarıdır; `null`
  atama "atamayı kaldır" demektir ve JSON'da yokluk ile ayrılamaz (ADR-0027)
- Satır sürümü yalnızca `PATCH /tickets/{id}` için zorunludur; durum zaten
  durum makinesiyle, atama üyelik kontrolüyle korunur (ADR-0028)
- `TaskItem` / `TaskItemStatus` adları `System.Threading.Tasks` ile çakışmayı
  önler; tel üzerindeki biçim değişmez (ADR-0029)
- Görevde durum makinesi ve iyimser eşzamanlılık **yoktur**; `PATCH` tüm
  alanları değiştirir ve `null` "yok" demektir (ADR-0030)
- Dashboard tek yanıt, önbelleksiz ve grafik kütüphanesiz; "açık talep"
  bitmemiş demektir ve üye sayımı elle kapsanır (ADR-0031)
- Tek topic exchange, mesaj tipi başına kuyruk; abonelik `StartAsync`'te
  kurulur; tüketiciler yalnızca Worker'da koşar; broker hazırlıkta `Degraded`
  döner, `Unhealthy` değil (ADR-0032)
- Use case'ler broker'a **yazmaz**: `IMessagePublisher` kendi transaction'ına
  outbox satırı ekler, `IBrokerPublisher`'ı yalnızca Worker'daki işleyici
  çağırır; idempotency host'ta, `ProcessedMessages` anahtarı mesaj **ve**
  tüketici (ADR-0033)
- Tüketicide veritabanı yazması **önce**, e-posta gönderimi **en son**; atama
  e-posta üretir, yorum üretmez; bildirim listesi her zaman çağıranın kendi
  bildirimleridir (ADR-0034)
- Depolama anahtarı sunucuda üretilir ve yüklenen adı içermez; içerik tipi
  beyaz listeyle doğrulanır (SVG hariç); her indirme API'den geçer, imzalı
  bağlantı verilmez (ADR-0035)
- Denetim kaydı senkron ve iş değişikliğiyle aynı transaction'da; ekleme-
  yalnızca; özneye ve aktöre yabancı anahtar yok; yük hassas veri taşımaz
  (ADR-0036)
- Önbellek bir hızlandırma, bağımlılık değil: hata yutulur, geçersiz kılma EF
  interceptor'ında, önbellek yokluğu desteklenen yapılandırma (ADR-0037)
- Tek seferlik geçişler (refresh token, davet) koşullu güncellemeyle; ekip
  değişiklikleri çalışma alanı satırında `FOR NO KEY UPDATE` kilidiyle ve
  çağıranın taze rolüyle; eşzamanlı yenilemeyi kaybeden `401
  auth.session_superseded` alır, aile iptal edilmez (ADR-0038)
- İki hostun da kaydettiği servisler `ITenantContext`'i **isteğe bağlı** çözer;
  Worker bileşimi tek çağrı (`AddFlowDeskWorker`) ve testle doğrulanır
  (ADR-0039)
- Çalışma alanına bağlı her uç nokta `EndpointCatalog`'da; yeni uç nokta
  katalog kaydı ve en düşük rolüyle birlikte eklenir, yoksa kapsam testi düşer
  (ADR-0040). Ek silme Admin'e ait
- Tarayıcı testleri gerçek API, Worker ve üretim derlemesini kendi
  portlarında başlatır; hazırlık API'den, zaman `page.clock` ile; rate limit
  değerleri yapılandırılabilir, varsayılanlar üretim değerleri (ADR-0041)
- Sekmeler arası yenileme `navigator.locks` ile sıraya girer (ADR-0038
  güncellemesi)

Kiracı izolasyonu bir **güvenlik sınırıdır**. Kullanıcı arayüzü Türkçe, kaynak
kod tanımlayıcıları İngilizce, commit mesajları Türkçe.

## Değiştirilen Önemli Dosyalar

Faz 17'de eklenenler / değişenler:

- `e2e/` — `playwright.config.ts`, `support/` (`environment`, `api`,
  `session`, `mailbox`, `global-setup`), `tests/` (oturum, sekmeler, destek
  yolculuğu, roller, davet, ayarlar)
- `backend/src/FlowDesk.Api/Common/RateLimitingPolicies.cs`, `ApiOptions.cs`
  — `RateLimitingSettings`
- `backend/src/FlowDesk.Application/Tickets/TicketMessages.cs`,
  `AddTicketComment/`, `Infrastructure/Notifications/TicketCommentedConsumer.cs`
  — yorum anındaki atanan kişi
- `frontend/src/lib/api/http-client.ts` — sekmeler arası yenileme kilidi
- `frontend/src/features/auth/auth-queries.ts` — çıkış
- `frontend/src/features/workspaces/workspace-settings-*.tsx`,
  `app/app/[workspaceSlug]/settings/page.tsx` — Ayarlar sayfası
- `frontend/src/features/workspaces/workspace-shell.tsx` — `<main>`
- `frontend/src/components/ui/dialog.tsx`, `sonner.tsx`,
  `features/notifications/notification-panel.tsx`,
  `features/tickets/ticket-attachments.tsx`,
  `features/customers/customer-form-dialog.tsx`,
  `features/team/invite-member-dialog.tsx` — erişilebilirlik ve metin
  düzeltmeleri
- `frontend/package.json` — `lint` önce `next typegen`
- `.gitignore`, `scripts/clean-sync-duplicates.sh`, `frontend/eslint.config.mjs`,
  `frontend/.prettierignore` — senkronizasyon kopyaları ve `.next.nosync`

Faz 16'da eklenenler / değişenler:

- `backend/tests/.../Hardening/` — `EndpointCatalog`, `EndpointProbe`,
  `EndpointCoverageTests`, `RoleMatrixTests`, `TenantBoundaryTests`,
  `ConcurrencyTests`
- `backend/tests/.../Authentication/SessionLifecycleTests.cs`
- `backend/tests/.../Messaging/BackgroundChainTests.cs`,
  `WorkerCompositionTests.cs`
- `backend/tests/.../Support/` — `TableGate`, `AdjustableClock`,
  `MailpitContainerFixture`; `PostgresContainerFixture.CreateIsolatedDatabaseAsync`;
  `FlowDeskApiFactory.Settings`
- `backend/tests/FlowDesk.UnitTests/Architecture/PermissionEnforcementTests.cs`
- `backend/src/FlowDesk.Infrastructure/WorkerServiceCollectionExtensions.cs`,
  `Notifications/NotificationServiceCollectionExtensions.cs` — Worker bileşimi
- `backend/src/FlowDesk.Worker/Program.cs` — tek çağrı
- `backend/src/FlowDesk.Infrastructure/Caching/DashboardCacheInvalidator.cs`,
  `Activity/ActivityRecorder.cs` — `ITenantContext` isteğe bağlı
- `backend/src/FlowDesk.Application/Authentication/RefreshSession/RefreshSessionHandler.cs`
- `backend/src/FlowDesk.Application/Team/AcceptInvitation/`,
  `RemoveMember/`, `ChangeMemberRole/`, `ListMembers/`, `MembershipQueries.cs`
- `backend/src/FlowDesk.Application/Abstractions/IFlowDeskDbContext.cs` —
  `LockTeamAsync`
- `backend/src/FlowDesk.Application/Tenancy/WorkspaceAction.cs` —
  `DeleteAttachments`
- `frontend/src/features/tickets/ticket-attachments.tsx` — silme yalnızca
  Admin/Owner; 13 dosyada Prettier biçimi

Faz 15'te eklenenler:

- `infra/docker-compose.yml` — `redis` servisi (kalıcılık kapalı)
- `.env.example` — `Cache__*` değişkenleri
- `backend/src/FlowDesk.Application/Abstractions/ICache.cs` — sözleşme ve
  `CacheKeys`
- `backend/src/FlowDesk.Application/Dashboard/IDashboardCachePolicy.cs`
- `backend/src/FlowDesk.Infrastructure/Caching/` — `CacheOptions`,
  `RedisCache`, `NullCache`, `DashboardCachePolicy`,
  `DashboardCacheInvalidator`
- `backend/tests/.../Support/RedisContainerFixture.cs`
- `backend/tests/.../Caching/` — dashboard önbelleği ve kayıt testleri

Faz 14'te eklenenler:

- `backend/src/FlowDesk.Domain/Activity/` — `ActivityEvent`, `ActivityType`,
  `ActivitySubject`
- `backend/src/FlowDesk.Application/Activity/` — `IActivityRecorder`,
  yükler, `ListActivityHandler`
- `backend/src/FlowDesk.Infrastructure/Activity/ActivityRecorder.cs`
- `backend/src/FlowDesk.Api/Endpoints/ActivityEndpoints.cs`
- On altı use case'e `IActivityRecorder` eklendi
- `backend/tests/.../Activity/ActivityTests.cs`
- `frontend/src/features/activity/` — ekran, veri katmanı
- `frontend/src/app/app/[workspaceSlug]/activity/page.tsx`

Faz 13'te eklenenler:

- `infra/docker-compose.yml` — `azurite` servisi ve `azurite-data` volume
- `.env.example` — `Storage__*` değişkenleri
- `backend/src/FlowDesk.Application/Abstractions/IFileStorage.cs`
- `backend/src/FlowDesk.Application/Tickets/AttachmentRules.cs` — beyaz liste,
  ad temizleme, anahtar üretimi
- `backend/src/FlowDesk.Application/Tickets/{Upload,Download,List,Delete}Attachment/`
- `backend/src/FlowDesk.Domain/Tickets/Attachment.cs`
- `backend/src/FlowDesk.Infrastructure/Storage/` — `StorageOptions`,
  `BlobFileStorage`, `AttachmentLimits`
- `backend/tests/.../Support/AzuriteContainerFixture.cs`
- `backend/tests/.../Attachments/AttachmentTests.cs`,
  `backend/tests/FlowDesk.UnitTests/Tickets/AttachmentRulesTests.cs`
- `frontend/src/lib/api/http-client.ts` — çok parçalı gövde ve `apiDownload`
- `frontend/src/features/tickets/ticket-attachments.tsx`

Faz 12'de eklenenler:

- `infra/docker-compose.yml` — `mailpit` servisi ve `mailpit-data` volume
- `.env.example` — `Email__*` değişkenleri (örnek değerler)
- `backend/src/FlowDesk.Application/Abstractions/IEmailSender.cs`
- `backend/src/FlowDesk.Application/Tickets/TicketMessages.cs`,
  `Team/TeamMessages.cs` — üç entegrasyon mesajı
- `backend/src/FlowDesk.Application/Notifications/` — liste ve okundu işaretleme
- `backend/src/FlowDesk.Domain/Notifications/` — `Notification`,
  `NotificationType`
- `backend/src/FlowDesk.Infrastructure/Email/` — `EmailOptions`,
  `SmtpEmailSender`
- `backend/src/FlowDesk.Infrastructure/Notifications/` — üç tüketici,
  `EmailBodies`, `NotificationPayloads`
- `backend/src/FlowDesk.Api/Endpoints/NotificationEndpoints.cs`
- `backend/src/FlowDesk.Worker/Program.cs` — tüketici kayıtları
- `backend/tests/.../Notifications/` — API ve tüketici testleri
- `frontend/src/features/notifications/` — panel ve veri katmanı
- `frontend/src/components/product/app-sidebar.tsx` — bildirim yuvası

Faz 11'de eklenenler:

- `backend/src/FlowDesk.Domain/Messaging/` — `OutboxMessage`, `ProcessedMessage`
- `backend/src/FlowDesk.Infrastructure/Messaging/` — `OutboxMessagePublisher`,
  `IBrokerPublisher`, `RabbitMqBrokerPublisher` (eski
  `RabbitMqMessagePublisher`), `OutboxDrain`, `OutboxProcessor`,
  `OutboxOptions`, `MessageIdempotency`
- `backend/src/FlowDesk.Infrastructure/Persistence/Configurations/` — outbox ve
  işlenmiş mesaj yapılandırmaları
- `backend/src/FlowDesk.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
  — `ITenantContext` artık isteğe bağlı çözülüyor (Worker için zorunlu)
- `backend/tests/.../Messaging/OutboxTests.cs`
- `.env.example` — `Outbox__*` değişkenleri

Faz 10'da eklenenler:

- `infra/docker-compose.yml` — `rabbitmq` servisi ve `rabbitmq-data` volume
- `.env.example` — `RABBITMQ_*` ve `Messaging__*` değişkenleri (örnek değerler)
- `backend/src/FlowDesk.Application/Abstractions/IMessagePublisher.cs` —
  sözleşme ve `IntegrationMessage` taban kaydı
- `backend/src/FlowDesk.Infrastructure/Messaging/` — bağlantı, yayınlayıcı,
  tüketici sözleşmeleri, abonelik, barındırılan servis, JSON ayarları,
  `MessageFormatException`
- `backend/src/FlowDesk.Infrastructure/HealthChecks/RabbitMqHealthCheck.cs`
- `backend/src/FlowDesk.Worker/Program.cs` — tüketici barındırma
- `backend/tests/.../Support/RabbitMqContainerFixture.cs`
- `backend/tests/.../Messaging/` — yayınlama, yönlendirme ve tüketici testleri

Faz 09'da eklenenler:

- `backend/src/FlowDesk.Application/Dashboard/` — `DashboardModels`,
  `GetDashboardHandler`
- `backend/src/FlowDesk.Api/Endpoints/DashboardEndpoints.cs`,
  `Contracts/DashboardContracts.cs`
- `backend/tests/.../Dashboard/DashboardTests.cs`
- `frontend/src/features/dashboard/` — ekran, veri katmanı, dağılım çubuğu
- `frontend/src/features/workspaces/workspace-dashboard.tsx` — yer tutucu
  kaldırıldı, gerçek ekrana bağlandı

Faz 08'de eklenenler:

- `backend/src/FlowDesk.Domain/Tasks/` — `TaskItem`, `TaskItemStatus`
- `backend/src/FlowDesk.Application/Tasks/` — altı use case, `TaskGuards`,
  `TaskWorkflow`, `TaskQueries`, `TaskErrors`
- `backend/src/FlowDesk.Infrastructure/Persistence/Configurations/TaskItemConfiguration.cs`
- `backend/src/FlowDesk.Api/Endpoints/TaskEndpoints.cs`,
  `Contracts/TaskContracts.cs`
- `backend/tests/.../Tasks/` — birim ve entegrasyon testleri
- `backend/tests/FlowDesk.IntegrationTests/Support/TestWorkspace.cs` —
  `TicketWorkspace`'ten yeniden adlandırıldı; artık görev testleri de kullanıyor
- `frontend/src/features/tasks/` — liste, satır bileşeni, form, müşteri sekmesi
- `frontend/src/app/app/[workspaceSlug]/tasks/page.tsx`

Faz 07'de eklenenler:

- `backend/src/FlowDesk.Domain/Tickets/` — `Ticket`, `TicketComment`,
  `TicketStatus`, `TicketNumber`, `TenantCounter`
- `backend/src/FlowDesk.Application/Tickets/` — dokuz use case,
  `TicketGuards`, `TicketWorkflow`, `TicketQueries`, `TicketErrors`
- `backend/src/FlowDesk.Infrastructure/Persistence/Configurations/` — üç
  yapılandırma; `FlowDeskDbContext` içinde `TakeNextTicketNumberAsync`
  (`FOR UPDATE`) ve `ExecuteInTransactionAsync`
- `backend/src/FlowDesk.Api/Endpoints/TicketEndpoints.cs`,
  `Contracts/TicketContracts.cs`
- `backend/tests/.../Tickets/` — birim ve entegrasyon testleri
- `frontend/src/features/tickets/` — liste, detay, tablo, form, yorumlar,
  müşteri sekmesi paneli
- `frontend/src/app/app/[workspaceSlug]/tickets/` — iki rota

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
| Son migration | `AddActivityEvents` |
| Migration uygulandı mı | Evet — yerel `flowdesk` veritabanına uygulandı |
| Tablolar | Identity kullanıcı tabloları, `RefreshTokens`, `Tenants`, `Memberships`, `Invitations`, `Customers`, `Tickets`, `TicketComments`, `TenantCounters`, `Tasks`, `OutboxMessages`, `ProcessedMessages`, `Notifications`, `Attachments`, `ActivityEvents` |
| Seed | Yok (Faz 21) |

Identity rol tabloları bilinçli olarak oluşturulmadı (ADR-0022).

Entegrasyon testleri kendi Testcontainers örneğini kullanır ve migration'ları
fixture içinde uygular; yerel veritabanına dokunmaz.

## Infrastructure Durumu

| Servis | Durum |
|---|---|
| PostgreSQL | **Aktif** — `flowdesk-postgres`, host portu 5433 |
| RabbitMQ | **Aktif** — `flowdesk-rabbitmq`, host portları 5672 / 15672 (yönetim arayüzü) |
| Mailpit | **Aktif** — `flowdesk-mailpit`, SMTP 1025, arayüz 8025. Faz 16'dan beri uçtan uca testte de container olarak koşuyor |
| Azurite | **Aktif** — `flowdesk-azurite`, blob 10000 (yalnızca blob servisi) |
| Redis | **Aktif** — `flowdesk-redis`, host portu 6380, kalıcılık kapalı |
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
   npm --prefix e2e ci && npm --prefix e2e test
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
