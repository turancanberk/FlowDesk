# FlowDesk — Mimari Kararlar (ADR)

Her kayıt hafif ADR biçimindedir: **Bağlam → Karar → Gerekçe → Sonuçlar**.

Kararlar sessizce tersine çevrilmez. Bir karar değişirse ilgili ADR "Değiştirildi"
olarak işaretlenir ve yerine geçen ADR referans verilir.

| No | Karar | Durum |
|---|---|---|
| ADR-0001 | Modüler monolit | Kabul edildi |
| ADR-0002 | Paylaşılan veritabanı + paylaşılan şema çok kiracılılık | Kabul edildi |
| ADR-0003 | User / Tenant / Membership kimlik modeli | Kabul edildi |
| ADR-0004 | MediatR, generic repository ve UnitOfWork kullanılmaması | Kabul edildi |
| ADR-0005 | Yol tabanlı workspace rotalama | Kabul edildi |
| ADR-0006 | Bellekte access token + dönen refresh token çerezi | Kabul edildi |
| ADR-0007 | Yabancı kiracı kaynağı için 404 | Kabul edildi |
| ADR-0008 | Kademeli altyapı devreye alma | Kabul edildi |
| ADR-0009 | İstemci tarafı kimlikli veri çekme sınırı | Kabul edildi |
| ADR-0010 | .NET 10 LTS hedefleme | Kabul edildi |
| ADR-0011 | TypeScript 6 hattı (7 değil) | Kabul edildi |
| ADR-0012 | Yalnızca Customer için soft delete | Kabul edildi |
| ADR-0013 | Yalnızca Ticket için iyimser eşzamanlılık | Kabul edildi |
| ADR-0014 | Outbox Pattern ve at-least-once idempotency | Kabul edildi |
| ADR-0015 | Redis yalnızca dashboard önbelleği için | Kabul edildi |
| ADR-0016 | Merkezî paket sürüm yönetimi ve sürüm doğrulama | Kabul edildi |
| ADR-0017 | ESLint 9 hattı (10 değil) | Kabul edildi |
| ADR-0018 | xUnit v3 ve Microsoft.Testing.Platform | Kabul edildi |
| ADR-0019 | Çözüm dosyası biçimi olarak .slnx | Kabul edildi |
| ADR-0020 | shadcn/ui kaynak bileşenleri + Base UI primitifleri | Kabul edildi |
| ADR-0021 | IFlowDeskDbContext ile EF Core sınırı | Kabul edildi |
| ADR-0022 | Kullanıcı hesabı Identity'ye ait, Domain'de User yok | Kabul edildi |
| ADR-0023 | API'de enum'lar ada göre serileştirilir | Kabul edildi |
| ADR-0024 | Global query filter mekanizması ve Membership istisnası | Kabul edildi |
| ADR-0025 | Türkçe arama için katlanmış SearchIndex sütunu | Kabul edildi |
| ADR-0026 | TanStack Table v8 hattı (v9 değil) | Kabul edildi |

---

## ADR-0001 — Modüler monolit

**Bağlam.** FlowDesk çok kiracılı bir SaaS ürünü. Mikroservis mimarisi
portfolyoda etkileyici görünebilir, ancak tek ekip tarafından geliştirilen ve
yüksek veri tutarlılığı gerektiren bir üründe gerçek bir problem çözmüyor.

**Karar.** Tek dağıtım birimi, içeride net modül sınırları olan modüler monolit.
Mikroservis ve Kubernetes kullanılmayacak.

**Gerekçe.** Modüller arası çağrılar süreç içi kalır; ağ hatası, dağıtık
transaction ve servis keşfi problemleri doğmaz. Sınırlar proje ve klasör
düzeyinde korunduğu için ileride gerçekten gerekirse ayrıştırma mümkün.

**Sonuçlar.** Tek veritabanı transaction'ı ile tutarlılık kolay. Buna karşılık
modül sınırlarını koruma sorumluluğu disipline kalır; bu yüzden bağımlılık yönü
mimari testlerle otomatik doğrulanır.

---

## ADR-0002 — Paylaşılan veritabanı + paylaşılan şema

**Bağlam.** Çok kiracılılık için üç yaygın seçenek var: kiracı başına
veritabanı, kiracı başına şema, paylaşılan şema.

**Karar.** Paylaşılan veritabanı ve paylaşılan şema. Kiracıya ait tüm iş
varlıkları `TenantId` sütunu taşır.

**Gerekçe.** Küçük ve orta ölçekli müşteri kitlesi için en düşük operasyon
maliyeti. Migration tek seferde uygulanır; yüzlerce şemayı senkron tutma yükü
yoktur. Kaynak kullanımı kiracı sayısıyla doğrusal artmaz.

**Sonuçlar.** İzolasyon fiziksel değil mantıksaldır; bu yüzden uygulama
katmanında üç savunma katmanı zorunludur (üyelik doğrulaması, global query
filter, yazma tarafı sahiplik kontrolü) ve izolasyon testleri kritik önemdedir.
Ayrıntı: `docs/SECURITY.md`.

---

## ADR-0003 — User / Tenant / Membership kimlik modeli

**Bağlam.** Bir kullanıcının birden fazla organizasyona farklı rollerle üye
olabilmesi gerekiyor.

**Karar.** `User`, `Tenant` ve aralarında rolü taşıyan `Membership` varlığı.
`User` üzerinde kalıcı tek bir `TenantId` **bulunmaz**.

**Gerekçe.** Rol kullanıcının kendisine değil, kullanıcı ile organizasyon
ilişkisine aittir. Aynı kişi Acme'de Owner, Kuzey'de Agent olabilir. Rolü
`User` üzerine koymak bu gerçeği modelleyemez.

**Sonuçlar.** Her tenant kapsamlı istekte `Membership` araması gerekir; bu
yüzden `(UserId, TenantId)` üzerinde benzersiz indeks tutulur. Kullanıcı
workspace'ler arasında geçiş yapabilir.

---

## ADR-0004 — MediatR, generic repository ve UnitOfWork kullanılmaması

**Bağlam.** .NET ekosisteminde bu üç yapı sık kullanılıyor ve "temiz mimari"
ile özdeşleştiriliyor.

**Karar.** Üçü de kullanılmayacak. Use case sınıfları doğrudan DI ile enjekte
edilecek; EF Core doğrudan ve bilinçli kullanılacak.

**Gerekçe.**
- **MediatR**: use case sınıfları zaten doğrudan enjekte edilebilir. Aracı
  katman çağrı zincirini gizler, hata ayıklamayı zorlaştırır ve karşılığında
  bu ölçekte bir fayda üretmez.
- **Generic `IRepository<T>`**: `DbSet<T>` zaten bu arayüzü sağlar. Üzerine
  sarmalayıcı koymak projeksiyon, `Include`, `AsNoTracking` ve sorgu
  bileşimini köreltir; sonuçta ya sarmalayıcı sızdırır ya da verimsiz sorgu
  yazılır.
- **Generic `UnitOfWork`**: `DbContext` zaten Unit of Work desenidir. İkinci bir
  soyutlama yalnızca dolaylılık ekler.

**Sonuçlar.** Kod daha az katmanlı ve izlenebilir. Buna karşılık EF Core
kullanımının kalitesi doğrudan görünür hale gelir; N+1, gereksiz tracking ve
aşırı `Include` gibi hatalara karşı inceleme disiplini gerekir.

---

## ADR-0005 — Yol tabanlı workspace rotalama

**Bağlam.** Workspace kimliği URL'de nasıl taşınacak? Seçenekler: wildcard
subdomain (`acme.flowdesk.app`), yol tabanlı (`/app/acme/...`), veya oturum
durumu.

**Karar.** Yol tabanlı rotalama. Frontend `/app/{workspaceSlug}/...`, API
`/api/workspaces/{workspaceSlug}/...`.

**Gerekçe.** Wildcard subdomain DNS yapılandırması, wildcard TLS sertifikası ve
çerez kapsamı karmaşıklığı getirir. Yol tabanlı yaklaşım yerelde ek yapılandırma
olmadan çalışır ve workspace bağlamını URL'de açıkça görünür kılar.

**Sonuçlar.** Kullanıcı URL'yi elle değiştirebilir; bu yüzden slug **hiçbir
zaman** doğrulanmadan kabul edilmez, her istekte üyelik doğrulanır.
Subdomain gerekirse ileride eklenebilir.

---

## ADR-0006 — Bellekte access token + dönen refresh token çerezi

**Bağlam.** Token saklama için yaygın seçenekler: her iki token'ı
`localStorage`'da tutmak, her ikisini çerezde tutmak, veya access token'ı
bellekte + refresh token'ı çerezde tutmak.

**Karar.** Access token yalnızca tarayıcı belleğinde tutulur ve
`Authorization: Bearer` ile gönderilir. Refresh token `HttpOnly` + `Secure` +
`SameSite=Strict` çerezde taşınır, döner (rotating), sunucuda hash'li saklanır
ve replay tespiti yapılır.

**Gerekçe.** `localStorage` JavaScript'e açıktır; bir XSS açığında token kalıcı
olarak sızdırılır. Bellekte tutulan token sayfa yenilenmesiyle kaybolur ve
saldırgan için kalıcı bir hedef oluşturmaz. Refresh token'ın `HttpOnly` çerezde
olması JavaScript erişimini tamamen keser. Rotation, çalınan bir token'ın
ikinci kullanımını tespit edilebilir kılar.

**Sonuçlar.**
- Access token başlıkla taşındığı için normal API çağrılarında CSRF yüzeyi
  yoktur; CSRF savunması yalnızca çerez taşıyan refresh/logout uç noktalarına
  odaklanır.
- Sayfa yenilendiğinde sessiz refresh gerekir; bu, ilk boyamada kısa bir
  kimlik doğrulama gecikmesi anlamına gelir.
- Server Component'lar access token'a erişemez (bkz. ADR-0009).
- Access token için kara liste tutulmaz; kısa TTL bilinçli bir ödünleşimdir.

Ayrıntı: `docs/SECURITY.md`.

---

## ADR-0007 — Yabancı kiracı kaynağı için 404

**Bağlam.** Bir kullanıcı üye olmadığı bir tenant'ın kaynağını istediğinde hangi
HTTP kodu dönmeli?

**Karar.** `404 Not Found` döner, `403 Forbidden` değil.

**Gerekçe.** `403`, kaynağın var olduğunu doğrular. Saldırgan kimlikleri
numaralandırarak hangi kayıtların var olduğunu öğrenebilir. `404` bu bilgiyi
vermez.

**Sonuçlar.** Hata ayıklama sırasında "yetki yok" ile "kayıt yok" ayrımı
yanıttan anlaşılmaz; bu ayrım sunucu log'unda tutulur.

---

## ADR-0008 — Kademeli altyapı devreye alma

**Bağlam.** Hedef mimari PostgreSQL, Redis, RabbitMQ, Mailpit, Azurite,
Prometheus ve Grafana içeriyor. Hepsini ilk fazda Compose'a eklemek mümkün.

**Karar.** Bir bileşen, onu gerçekten kullanan faz gelmeden `docker-compose.yml`
dosyasına eklenmeyecek. Sıra: PostgreSQL (Faz 01) → RabbitMQ (10) →
Mailpit (12) → Azurite (13) → Redis (15) → Prometheus/Grafana (18).

**Gerekçe.** Kullanılmayan servisi erken ayağa kaldırmak yerel geliştirme
ortamını gereksiz ağırlaştırır ve her bileşenin gerçek bir varlık sebebi olup
olmadığını gizler. Bu makinede Docker VM'ine 8 GB ayrılmış ve başka projelerin
konteynerleri de çalışıyor; kaynak baskısı somut bir risk.

**Sonuçlar.** Hedef mimari `docs/ARCHITECTURE.md` içinde önceden belgelenir,
ancak Compose dosyası her fazda yalnızca gerçekten kullanılanı içerir.
Gözlemlenebilirlik yığını Faz 18'de Compose profile arkasına konur ve günlük
geliştirmede isteğe bağlı kalır.

---

## ADR-0009 — İstemci tarafı kimlikli veri çekme sınırı

**Bağlam.** ADR-0006 gereği access token yalnızca tarayıcı belleğinde. Next.js
Server Component'ları sunucuda çalışır ve bu belleğe erişemez.

**Karar.** Kimlik doğrulaması gerektiren tüm veri çekme işlemleri istemci
tarafında yapılır: Client Component + TanStack Query + `Authorization: Bearer`
başlığı ekleyen tek bir HTTP istemcisi. Server Component'lar yalnızca kimlik
gerektirmeyen kabuk işini üstlenir (düzen, statik içerik, metadata, rota
yapısı, iskelet durumları) ve korumalı API'ye çağrı yapmaz.

**Gerekçe.** Alternatif, Next.js'i BFF (Backend for Frontend) olarak kullanıp
token'ı sunucu tarafında tutmaktı. Bu, her istek için ek bir ağ atlaması, ikinci
bir oturum katmanı ve iki yerde yetkilendirme mantığı anlamına gelirdi.
Seçilen yaklaşım token'ı hiçbir kalıcı depoya yazmaz ve tek bir yetkilendirme
sınırı bırakır: backend.

**Sonuçlar.**
- İlk boyamada sunucu tarafı veri yoktur; korumalı ekranlar iskelet (skeleton)
  durumuyla açılır.
- Rota koruması istemci tarafı bir guard ile yapılır. Bu bir **kullanıcı
  deneyimi katmanıdır, güvenlik sınırı değildir**; gerçek sınır her zaman
  backend yetkilendirmesidir.
- Korumalı sayfalar için SEO gereksinimi yoktur, bu yüzden sunucu tarafı
  render kaybı bir maliyet oluşturmaz.

---

## ADR-0010 — .NET 10 LTS hedefleme

**Bağlam.** Geliştirme makinesinde .NET 8 ve 9 SDK'ları kurulu, 10 kurulu
değildi. .NET 9 bakım aşamasında ve desteği 10 Kasım 2026'da bitiyor.

**Karar.** .NET 10 LTS hedeflenecek. SDK resmî `dotnet-install.sh` ile
`~/.dotnet` altına yan yana kuruldu; mevcut 8 ve 9 SDK'larına dokunulmadı.
Sürüm `backend/global.json` ile sabitlendi.

**Gerekçe.** .NET 10 desteği 2028-11-14'e kadar sürüyor. Ekosistem paketleri
(EF Core, Npgsql, Serilog, ASP.NET Core Identity) 10.x hattında güncel. Bir
portfolyo projesinin iki ay içinde destek dışı kalan bir sürümü hedeflemesi
anlamsız olurdu.

**Sonuçlar ve bilinen ortam sorunu.** macOS 26 (Apple Silicon) üzerinde
`dotnet-install.sh` mevcut bir `~/.dotnet` kurulumunun üzerine yazdığında, host
ikilisi `SIGKILL (Code Signature Invalid)` ile öldürülebiliyor. Crash raporunda
sonlanma nedeni `CODESIGNING / Taskgated Invalid Signature` olarak görünür.
Dosyanın imzası `codesign -v` ile geçerlidir; sorun çekirdeğin o inode için
tuttuğu bayat imza değerlendirmesidir. Çözüm, host ikilisini yeni bir inode ile
değiştirmektir:

```bash
cd ~/.dotnet && cp dotnet dotnet.new && mv -f dotnet.new dotnet && chmod +x dotnet
```

---

## ADR-0011 — TypeScript 6 hattı (7 değil)

**Bağlam.** npm'de TypeScript'in `latest` etiketi 7.x sürümünü gösteriyor.

**Karar.** TypeScript 6.0.x hattı kullanılacak.

**Gerekçe.** `typescript-eslint` 8.x paketinin peer bağımlılık aralığı
`typescript >=4.8.4 <6.1.0`. TypeScript 7 seçilseydi tip farkındalıklı ESLint
kuralları tamamen çalışmaz hale gelirdi. TypeScript 6.0.x stabil, güncel ve lint
zinciriyle tam uyumlu.

**Sonuçlar.** En yeni TypeScript sürümü kullanılmıyor. `typescript-eslint` peer
aralığı genişlediğinde karar yeniden değerlendirilecek.

---

## ADR-0012 — Yalnızca Customer için soft delete

**Bağlam.** Soft delete her varlığa uygulanabilir, ancak her yerde sorgu
karmaşıklığı ve unutulan filtre riski yaratır.

**Karar.** Yalnızca `Customer` soft delete (arşivleme) destekleyecek. Diğer
varlıklar kalıcı olarak silinecek.

**Gerekçe.** Müşteri kayıtları geçmiş taleplerin ve görevlerin bağlamını taşır.
Bir müşteriyi kalıcı silmek bu geçmişi anlamsız kılar; ürün açısından doğru
davranış arşivlemektir. Talep yorumu veya görev gibi varlıklarda böyle bir
bağlam bağı yoktur.

**Sonuçlar.** `Customer` üzerinde global query filter ile arşivlenmişler
varsayılan olarak gizlenir. Arşivlenmiş müşteriler ayrı bir filtreyle
listelenebilir.

---

## ADR-0013 — Yalnızca Ticket için iyimser eşzamanlılık

**Bağlam.** İki kullanıcının aynı kaydı aynı anda düzenlemesi mümkün.

**Karar.** Yalnızca `Ticket` üzerinde iyimser eşzamanlılık uygulanacak
(PostgreSQL `xmin` sistem sütunu). Çakışmada `409 Conflict` döner.

**Gerekçe.** Talep, birden fazla kişinin aynı anda dokunma olasılığı en yüksek
kayıttır: biri durumu değiştirirken diğeri atama yapabilir. Müşteri ve görev
kayıtlarında bu senaryo gerçekçi değil; her varlığa eşzamanlılık belirteci
eklemek gereksiz karmaşıklık olurdu.

**Sonuçlar.** Talep güncellemelerinde istemci çakışma durumunu ele almalı ve
kullanıcıya Türkçe bir uyarı göstererek yeniden yüklemeyi önermelidir.

---

## ADR-0014 — Outbox Pattern ve at-least-once idempotency

**Bağlam.** Bir iş değişikliği kaydedildiğinde bağlı bir entegrasyon olayının
(e-posta, bildirim) da yayınlanması gerekiyor. Veritabanı yazma ile mesaj
yayınlama iki ayrı sistemdir; arada hata olursa tutarsızlık doğar.

**Karar.** Outbox Pattern uygulanacak. `OutboxMessage` kaydı, iş değişikliğiyle
**aynı transaction** içinde yazılır. `Worker` bekleyen kayıtları
`FOR UPDATE SKIP LOCKED` ile alıp RabbitMQ'ya yayınlar. Tüketiciler
`ProcessedMessage` tablosu ile idempotent yapılır.

**Gerekçe.** Aynı transaction, "veri kaydedildi ama mesaj gitmedi" veya "mesaj
gitti ama veri kaydedilmedi" durumlarını ortadan kaldırır. `SKIP LOCKED` birden
fazla worker örneğinin aynı kaydı işlemesini engeller. Yayınlama en az bir kez
(at-least-once) olduğu için tüketici tarafında idempotency zorunludur.

**Sonuçlar.** Mesaj teslimi anlık değil, kısa gecikmelidir. Başarısız kayıtlar
yeniden denenir ve hata görünür kalır; sessizce kaybolmaz.

---

## ADR-0015 — Redis yalnızca dashboard önbelleği için

**Bağlam.** Redis birçok yerde kullanılabilir: oturum, sorgu önbelleği,
rate limiting deposu, dağıtık kilit.

**Karar.** Redis yalnızca dashboard toplamlarını önbelleklemek için
kullanılacak. Anahtar şeması `tenant:{tenantId}:dashboard`, kısa TTL ve ilgili
veri değiştiğinde geçersiz kılma uygulanacak.

**Gerekçe.** Dashboard birden fazla toplama sorgusu çalıştırır ve sık açılır;
önbellek burada ölçülebilir bir fayda sağlar. Diğer kullanımlar için gerçek bir
problem yok: oturum zaten JWT ile durumsuz, rate limiting tek örnekte bellek
içi yeterli.

**Sonuçlar.** Dashboard kısa süreli bayat veri gösterebilir; TTL ve geçersiz
kılma davranışı Faz 15'te belgelenecek. Redis devre dışıyken uygulama
çalışmaya devam etmeli, yalnızca önbellek faydasını kaybetmelidir.

---

## ADR-0016 — Merkezî paket sürüm yönetimi ve sürüm doğrulama

**Bağlam.** Çok projeli bir solution'da paket sürümleri kolayca birbirinden
ayrışır. Ayrıca plan aşamasında not edilen sürümler zamanla eskir.

**Karar.** Backend sürümleri `Directory.Packages.props` ile merkezî olarak
yönetilecek (Central Package Management). Frontend `package-lock.json` depoda
tutulacak. Her paket kurulum anında resmî registry'den doğrulanacak; yalnızca
stable sürüm kullanılacak, `preview`/`rc`/`beta`/`canary` kullanılmayacak.
Kullanımdan kaldırılmış paketler ve bilinen ciddi açığı olan sürümler
elenecek.

**Gerekçe.** Merkezî yönetim sürüm ayrışmasını ve diamond dependency
problemlerini önler. Kurulum anında doğrulama, dokümandaki sürüm
numaralarının eskimesi riskini ortadan kaldırır.

**Sonuçlar.** Bu kapsamda alınan somut kararlar: `FluentValidation.AspNetCore`
kullanılmayacak (kullanımdan kalkmış otomatik doğrulama paketi); bunun yerine
çekirdek `FluentValidation` paketi minimal API endpoint filtresi ile açıkça
bağlanacak. Base UI için stabil paket adı `@base-ui/react`'tır;
`@base-ui-components/react` eski/RC isimlendirmedir ve kullanılmaz.


---

## ADR-0017 — ESLint 9 hattı (10 değil)

**Bağlam.** ESLint 10.10.0 güncel major sürüm. Ancak `eslint-config-next@16.3.5`
üç eklenti getiriyor: `eslint-plugin-import`, `eslint-plugin-jsx-a11y` ve
`eslint-plugin-react`. Üçünün de peer aralığı ESLint 9 ve öncesiyle sınırlı.
ESLint 10 ile kurulum yapıldığında npm bu peer'ları eziyor, ikinci bir ESLint
kopyası yükleniyor ve lint zinciri öngörülemez hale geliyor.

**Karar.** ESLint **9.39.5** sabitlenecek — Next.js lint zincirinin desteklediği
en yeni sürüm.

**Gerekçe.** ADR-0016'daki ilkeyle aynı: peer uyumsuzluğu bilerek kabul
edilmez. ESLint sürümünü zorlamak, tip farkındalıklı lint kurallarının sessizce
çalışmamasına yol açardı; oysa bu kurallar (`no-unsafe-*`,
`no-floating-promises`) projenin tip güvenliği kapısının bir parçası.

**Sonuçlar.** npm, ESLint 9.39.5 için "artık desteklenmiyor" uyarısı gösterir;
bu bir yaşam döngüsü bildirimidir, bilinen bir güvenlik açığı değildir
(`npm audit` temiz). Karar, Next.js lint zinciri ESLint 10'u desteklediğinde
yeniden değerlendirilecek.

---

## ADR-0018 — xUnit v3 ve Microsoft.Testing.Platform

**Bağlam.** .NET 10 SDK'nın `dotnet new xunit` şablonu hâlâ xUnit v2 üretiyor.
xUnit v3 güncel major sürüm ve Microsoft.Testing.Platform (MTP) üzerine kurulu.
.NET 10 SDK'da VSTest hedefi MTP tabanlı test projeleri için artık
desteklenmiyor ve açık bir hata veriyor.

**Karar.** xUnit **v3** kullanılacak. Çalıştırıcı seçimi kökteki `global.json`
içinde yapılır:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

**Gerekçe.** v3 aktif geliştirilen sürüm; şablonun v2 üretmesi yalnızca şablonun
güncellenmemiş olmasından kaynaklanıyor. MTP'ye geçmek, .NET 10'un desteklediği
yoldur.

**Sonuçlar.** VSTest tarafındaki paketlere gerek kalmaz:
`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio` ve `coverlet.collector`
kaldırıldı. Test projeleri çalıştırılabilir (`OutputType=Exe`) olarak derlenir.
Kod kapsamı gerekirse MTP eklentisiyle Faz 20'de eklenecek.

`global.json` depo kökündedir, `backend/` altında değil: `dotnet test` çalıştırıcı
yapılandırmasını çalışma dizininden yukarı doğru arar, bu yüzden depo kökünden
verilen komutların onu bulması gerekir.

---

## ADR-0019 — Çözüm dosyası biçimi olarak .slnx

**Bağlam.** .NET 10 SDK'da `dotnet new sln` artık klasik `.sln` yerine XML
tabanlı `.slnx` üretiyor.

**Karar.** `backend/FlowDesk.slnx` kullanılacak.

**Gerekçe.** `.slnx` .NET 10'un varsayılanı; okunabilir, birleştirme
çakışmalarına klasik `.sln` biçiminden çok daha az açık ve `dotnet`
komutlarının tamamı tarafından destekleniyor. SDK varsayılanına karşı gitmek
için bir sebep yok.

**Sonuçlar.** `.slnx` desteği Visual Studio 2022 17.13 ve sonrasını gerektirir.
CI, depoda sabitlenen .NET 10 SDK'sını kullandığı için etkilenmez.


---

## ADR-0020 — shadcn/ui kaynak bileşenleri + Base UI primitifleri

**Bağlam.** Erişilebilir bileşenler için üç yol vardı: her şeyi elle yazmak,
hazır bir bileşen kütüphanesini paket olarak kullanmak, ya da bileşen kaynağını
depoya alıp sahiplenmek.

Elle yazmak, diyalog odak tuzağı, menü klavye gezintisi ve select
konumlandırması gibi doğru yapılması zor işleri sıfırdan çözmek demekti. Paket
olarak kullanmak ise tasarım sistemini paketin görsel kararlarına bağlardı;
FlowDesk'in kendi token sistemi ve yoğunluk hedefi var.

**Karar.** shadcn CLI ile bileşenler **Base UI tabanlı** olarak (`--base base`)
depoya kaynak biçiminde eklenecek ve yerelde sahiplenilecek. Erişilebilirlik
primitifleri `@base-ui/react` paketinden gelir.

**Gerekçe.** Kaynak depoda olduğu için bileşen davranışı ve görünümü doğrudan
değiştirilebilir; bir kütüphane sürümünün arkasında beklemek gerekmez. Base UI,
odak yönetimi ve klavye gezintisi gibi zor kısımları çözer.

shadcn'in semantik değişken sözleşmesi (`--background`, `--primary`, `--border`,
`--ring` ...) korunur ve değerleri FlowDesk paletiyle doldurulur. Böylece
ileride eklenen bir bileşen yeniden boyanmadan doğru görünür. FlowDesk'e özgü
ek token'lar (`--canvas`, `--accent-surface`, anlamsal renk üçlüleri) bu
sözleşmenin yanında durur.

**Sonuçlar.**

- Üretilen bileşenlerde koyu tema (`dark:`) sınıfları kalır. Koyu tema çekirdek
  kapsam dışıdır (`docs/ROADMAP.md`) ve `.dark` sınıfı hiçbir yerde
  uygulanmadığı için bu sınıflar etkisizdir. Her bileşenden tek tek
  temizlemek, ileride koyu tema eklenirse geri yazılacak bir işi bugün yapmak
  olurdu.
- Varsayılan kurulumun getirdiği bazı seçimler FlowDesk kararlarıyla
  çakıştığı için değiştirildi: rozet varsayılanı hap biçiminden küçük yarıçapa
  çevrildi, tablo yoğunluğu 44 px satır ve 36 px başlığa ayarlandı, `Alert`
  bileşenine anlamsal ton varyantları eklendi, `Toaster` bileşeninin
  `next-themes` bağımlılığı kaldırıldı.
- shadcn paketi çalışma zamanı bağımlılığı değil, geliştirme aracıdır ve
  `devDependencies` altına alındı.


---

## ADR-0021 — IFlowDeskDbContext ile EF Core sınırı

**Bağlam.** ADR-0004 gereği generic repository ve UnitOfWork sarmalayıcısı
kullanılmıyor; EF Core doğrudan kullanılacak. Ancak ADR-0001 bağımlılık yönünü
tek yönlü tutuyor: Application, Infrastructure'a bağımlı olamaz. `DbContext`
bir altyapı tipi olduğu için use case'lerin ona doğrudan erişmesi bu kuralı
kırardı.

**Karar.** Application katmanında `IFlowDeskDbContext` arayüzü tanımlanacak.
Arayüz `DbSet<T>` döndürür ve `SaveChangesAsync` sunar. `FlowDeskDbContext`
bunu Infrastructure'da uygular.

Application projesi `Microsoft.EntityFrameworkCore` paketine bağımlı olacak,
ancak veritabanı **sağlayıcısına** (Npgsql) bağımlı olmayacak.

**Gerekçe.** Üç seçenek vardı:

1. Application'ın Infrastructure'a bağımlı olmasına izin vermek — katman
   hikâyesini tamamen çökertirdi.
2. Her varlık için odaklı repository yazmak — ADR-0004'ün kaçındığı dolaylılığı
   varlık başına geri getirirdi.
3. Tek bir context sözleşmesi.

Üçüncüsü seçildi çünkü tek bir mimari sınır, varlık başına bir sarmalayıcıdan
hem daha az hem de daha dürüst. `DbSet<T>` açıkta kaldığı için projeksiyon,
`AsNoTracking` ve sorgu bileşimi kaybolmuyor — repository yaklaşımının en pahalı
kaybı buydu.

EF Core'un Application'da bulunması bilinçli bir taviz. EF Core pratikte
değiştirilebilir bir bağımlılık değil; öyleymiş gibi davranmak sorgu ifade
gücünü gerçek bir karşılık almadan harcamak olurdu. Tutulan sınır sağlayıcı
sınırı: PostgreSQL'e özgü bir sorgunun use case içinde yazılmaması gerekir.

**Sonuçlar.** Bu sınır mimari testle korunuyor:
`Application_uses_EntityFrameworkCore_but_no_database_provider`. Test olmadan
sınır, ilk sağlayıcıya özgü yardımcıya ihtiyaç duyulduğunda sessizce
genişlerdi.

---

## ADR-0022 — Kullanıcı hesabı Identity'ye ait, Domain'de User yok

**Bağlam.** ASP.NET Core Identity kullanılıyor ve `IdentityUser<TKey>` bir EF
Core/altyapı tipi. Domain katmanı altyapıya bağımlı olamadığı için "Domain'de
ayrı bir `User` varlığı olmalı mı?" sorusu doğuyor.

**Karar.** `ApplicationUser : IdentityUser<Guid>` Infrastructure'da yaşayacak.
Domain'de `User` varlığı **olmayacak**. Domain varlıkları kullanıcıya `Guid`
ile referans verecek.

**Gerekçe.** Identity zaten parola hash'i, normalleştirilmiş e-posta, güvenlik
damgası ve kilitleme durumunu tutuyor. Paralel bir domain kullanıcısı bu
durumu ikizler ve senkron tutulması gereken ikinci bir yer yaratırdı; bu tür
ikizlemeler kaçınılmaz olarak birinde güncellenip diğerinde unutulur.

Kullanıcı hesabı bir kimlik/kimlik doğrulama kaydıdır, bir iş varlığı değil.
FlowDesk'in domain kuralları kullanıcının parolası hakkında hiçbir şey
söylemiyor; söylediği şey üyelik, atama ve sahiplik ki bunların hepsi
`UserId` ile ifade edilebiliyor.

**Sonuçlar.**

- `RefreshToken` gibi domain varlıkları `ApplicationUser`'a navigation property
  taşımaz. Yabancı anahtar, EF yapılandırmasında Infrastructure tarafında
  tanımlanır.
- Identity rol tabloları oluşturulmaz. Rol, hesabın değil üyeliğin özelliğidir
  (ADR-0003); bu yüzden `IdentityDbContext` yerine `IdentityUserContext`
  kullanıldı ve `AspNetRoles`, `AspNetUserRoles`, `AspNetRoleClaims`
  tablolarının kalıcı olarak boş durması engellendi.
- Application katmanı Identity'yi doğrudan tanımaz; `IUserAccountStore`
  sözleşmesi üzerinden çalışır ve bu sayede use case'ler veritabanı olmadan
  test edilebilir.


---

## ADR-0023 — API'de enum'lar ada göre serileştirilir

**Bağlam.** `System.Text.Json` varsayılan olarak enum'ları sayısal değeriyle
serileştirir. Faz 04'te çalışma alanı rolü istemciye `3` olarak gitti ve arayüz
`"Owner"` beklediği için çöktü.

**Karar.** `ConfigureHttpJsonOptions` ile `JsonStringEnumConverter` eklendi;
tüm enum'lar adlarıyla taşınır.

**Gerekçe.** Sayısal değer, sözleşmeyi bildirim sırasına bağlar. Bir rolü veya
talep durumunu enum'un ortasına eklemek, daha önce saklanmış ve yolda olan her
değerin anlamını sessizce değiştirirdi. Ad kullanmak bu bağı koparır, tel
biçimini okunur kılar ve `docs/API_CONVENTIONS.md`'nin istemcilere verdiği
sözü karşılar.

**Sonuçlar.** .NET test istemcisinin de aynı dönüştürücüyü kullanması gerekir;
`FlowDeskJson.Options` bunun için paylaşılan ayar noktasıdır. Regresyonu
`Roles_are_serialised_by_name` testi engelliyor — bu hata, bir istemci rolü
yanlış okuyana kadar görünmez kalırdı.

---

## ADR-0024 — Global query filter mekanizması ve Membership istisnası

**Bağlam.** Kiracıya ait her varlık, geçerli çalışma alanına göre süzülmeli.
Bunu varlık başına elle yazmak, yeni bir varlık eklendiğinde filtrenin
unutulmasına açıktır ve unutulan bir filtre çalışma alanları arası veri
sızıntısıdır.

**Karar.** `ITenantOwned` arayüzü bir işaretleyicidir. `OnModelCreating`,
modeldeki bu arayüzü uygulayan her tipe filtreyi otomatik uygular. Bir varlık
arayüzü uygulayarak kaydolur; ayrıca bir şey yapmaya gerek yoktur.

`Membership` bu filtrenin **dışındadır**.

**Gerekçe.** "Hangi çalışma alanlarına üyeyim?" sorusu tüm alanlara bakmak
zorundadır. Üyelikleri geçerli çalışma alanına göre süzmek bu soruyu
cevaplanamaz hâle getirirdi.

Filtre tipli bir lambda ile kurulur, elle inşa edilmiş ifade ağacıyla değil:
EF Core'un sorgu başına yeniden değerlendirdiği biçim tam olarak budur. Model
bir kez derlenip önbelleğe alındığı için, bağlamı değil bir değeri yakalayan
bir filtre ilk isteğin çalışma alanını sonraki tüm isteklere dondururdu.

**Sonuçlar.** Filtre ikinci savunma hattıdır, sınır değildir:
`IgnoreQueryFilters` onu atlar ve ham SQL hiç görmez. Birincil hat her istekte
yapılan üyelik doğrulamasıdır (`docs/SECURITY.md`).

Mekanizmanın doğru kurulduğu `TenantQueryFilterTests` ile model seviyesinde
doğrulanır; böylece garanti henüz var olmayan varlıklar için de geçerlidir.
İlk gerçek tüketicisi Faz 06'daki `Customer` olacak ve davranışsal test de
orada yazılacak.


---

## ADR-0025 — Türkçe arama için katlanmış SearchIndex sütunu

**Bağlam.** Müşteri araması ilk uygulamada `LOWER(sütun) LIKE LOWER(terim)`
biçimindeydi. Testler gerçek bir hatayı yakaladı: "YAZILIM" araması "Kuzey
Yazılım" kaydını bulamıyordu.

Türkçede iki ayrı i harfi var — noktalı (i/İ) ve noktasız (ı/I) — ve hiçbir
küçültme stratejisi kullanıcının beklediği sonucu vermiyor:

- Invariant küçültme "YAZILIM"ı noktalı i ile "yazilim" yapıyor, saklanan
  "Yazılım" ise noktasız ı ile "yazılım" oluyor. İkisi hiç eşleşmiyor.
- Kültüre duyarlı küçültme hangi çiftin bozulduğunu değiştiriyor ve sonucu
  sunucunun yereline bağlıyor.
- SQL `LOWER()` veritabanı harmanlamasına bağlı ve aynı sorunu yaşıyor.

**Karar.** `TurkishText.Fold` her iki tarafı da ASCII'ye katlıyor. `Customer`
üzerinde `SearchIndex` sütunu ad, şirket ve e-postanın katlanmış hâlini tutuyor;
arama bu sütunla tek bir karşılaştırma yapıyor.

**Gerekçe.** Katlama soruyu ortadan kaldırıyor: "yazilim", "YAZILIM" ve
"Yazılım" hepsi "yazilim" oluyor. Yan faydası, Türkçe karakter bulunmayan bir
klavyeden yazan kişinin de kaydı bulabilmesi — gerçek bir kullanım senaryosu.

Sütun olarak saklanması iki sebeple: katlama SQL'de doğru yapılamıyor, ve
saklanan bir sütun indekslenebilirken satır başına hesaplanan bir ifade
indekslenemez.

**Sonuçlar.** `SearchIndex` kullanıcıya hiç gösterilmiyor; yalnızca katlanmış
bir arama terimiyle karşılaştırılmak için var. Aynı katlama `WorkspaceSlug`
tarafından da kullanılıyor, böylece Türkçe harf haritası tek yerde duruyor.
Arama gerçek veride yavaşlarsa cevap ifade veya trigram indeksi olacak,
sağlayıcıya özgü bir operatör değil.

---

## ADR-0026 — TanStack Table v8 hattı (v9 değil)

**Bağlam.** npm'de `@tanstack/react-table` `latest` etiketi v9'u gösteriyor.
v9, özellik tabanlı yeni bir API getiriyor: `useReactTable` yerine `useTable`,
`getCoreRowModel` yerine `createCoreRowModel`, ve özelliklerin açıkça
birleştirilmesi.

**Karar.** v8 (8.21.3) kullanılacak.

**Gerekçe.** v9 paketi kullanım belgesi içermiyor; API'yi tip tanımlarından
çıkarmak gerekiyordu. Veri tabloları ürünün kimliği ve üç modülde daha
kullanılacak; en çok kullanılan bileşenin, çıkarımla öğrenilen bir API üzerine
kurulması kabul edilebilir değil.

v8 olgun, desteklenen ve shadcn DataTable dâhil tüm referans uygulamaların
dayandığı sürüm.

**Sonuçlar.** En yeni major sürüm kullanılmıyor. v9 belgelenip yaygınlaştığında
karar yeniden değerlendirilecek.

React derleyicisi `useReactTable`'ın döndürdüğü fonksiyonları güvenle
memoize edemiyor; TanStack'in belgelediği `"use no memo"` direktifi yalnızca
tabloyu render eden bileşende kullanılıyor, dosyanın kalanı derleyici
optimizasyonunu koruyor.

---

## ADR-0027 — Durum ve atama, PATCH alanı değil kendi eylem rotaları

**Bağlam.** `docs/API_CONVENTIONS.md` kısmi güncelleme için `PATCH` kullanır ve
talep rotaları arasında başlangıçta yalnızca `PATCH /tickets/{id}` vardı. Talep
üzerinde iki işlem bu kalıba oturmuyor:

- **Durum değişikliği** bir durum makinesi geçişi. Geçerliliği gönderilen değere
  değil, talebin o anki durumuna bağlı. Kapalı bir talep "işlemde" yapılamaz,
  ama aynı gövde açık bir talep için geçerli.
- **Atama kaldırma** `null` demek. JSON'da alanın *yokluğu* ile `null` değeri
  arasındaki fark, `System.Text.Json` ile bağlanan bir kayıtta güvenilir biçimde
  okunamaz; her ikisi de `null` olarak gelir. Yani `PATCH` gövdesi "atamayı
  değiştirme" ile "atamayı kaldır"ı ifade edemez.

**Karar.** `POST /tickets/{id}/status` ve `POST /tickets/{id}/assignment` ayrı
eylem rotaları olarak eklendi. `PATCH /tickets/{id}` geriye kalan alan
düzenlemesini yapıyor: konu, açıklama, öncelik, müşteri.

**Gerekçe.** Kural tablosundaki `POST` zaten "oluşturma **veya eylem**" olarak
tanımlı; bu iki işlem tam olarak eylem. Ayrı rota aynı zamanda yetkilendirme ve
hata kodlarını netleştiriyor: geçersiz geçiş `409` döndürüyor ve bu kod,
gövdesi kusursuz ama o an mümkün olmayan bir isteği doğru anlatıyor.

**Sonuçlar.** Sapma `docs/API_CONVENTIONS.md` içinde "Bilinçli sapmalar"
başlığına gerekçesiyle yazıldı. Arayüz tarafında bu ayrım kendiliğinden işe
yarıyor: detay ekranı durum ve atama için tek tıklık kontroller sunuyor,
düzenleme formu ise ayrı bir diyalog.

---

## ADR-0028 — Sürüm kontrolü yalnızca talep düzenlemesinde zorunlu

**Bağlam.** ADR-0013, `Ticket` üzerinde PostgreSQL `xmin` ile iyimser
eşzamanlılık kurdu. Açık kalan soru, her yazma işleminin istemciden bir satır
sürümü isteyip istemeyeceğiydi.

**Karar.** `PATCH /tickets/{id}` `version` alanını **zorunlu** tutuyor. Durum
değişikliği ve atama istemciden sürüm istemiyor.

**Gerekçe.** Kayıp güncellemenin zararlı olduğu yer serbest metin. İki kişi aynı
açıklamayı düzenlerse, ikincisinin kaydı birincinin yazdığını iz bırakmadan
siler. Bunu ancak istemcinin okuduğu sürüm engelliyor.

Durum ise zaten alan modelinin geçiş tablosuyla korunuyor. Bir meslektaş arada
talebi taşımışsa istenen hareket ya hâlâ geçerli, ya zaten olmuş bir işlemin
tekrarı, ya da reddediliyor. Üzerine yazılacak metin yok. Sürüm istemek burada
yalnızca tek tıklık bir eylemi karşılıksız biçimde başarısız kılardı.

Atamada son yazan kazanıyor ve bu doğru anlam: "bu talep artık Ayşe'nin" şimdiki
zaman hakkında bir cümle, sahipliği en son söyleyen haklı. Açılır listede çakışma
diyaloğu göstermek gürültü olurdu.

**Sonuçlar.** Sunucu yine de her yazmada `xmin` belirtecini taşıyor, çünkü EF
izlenen varlığı okuduğu sürümle güncelliyor. Yani okuma ile yazma arasındaki
çakışma her koşulda yakalanıyor; istemciden gelen sürüm bunu "form açık dururken
geçen süreye" genişletiyor. `TicketDetail` her yanıtta yeni sürümü döndürüyor,
böylece açık bir form ikinci kez kaydederken kendi önceki kaydına çakışma
bildirmiyor.

---

## ADR-0029 — `TaskItem` ve `TaskItemStatus` adlandırması

**Bağlam.** Ürün kavramının adı "Görev", domain karşılığı `Task`. Ancak `Task`
ve `TaskStatus` adlarının ikisi de `System.Threading.Tasks` altında var ve bu
ad alanı örtük using'lerle her dosyaya geliyor.

Çakışma gizlenmiyor, **belirsizlik** üretiyor: her iki ad da kapsamdayken
derleyici hangisinin kastedildiğini bilemiyor ve derleme kırılıyor. Her async
imzada tam nitelikli ad yazmak ya da her dosyada takma ad tanımlamak gerekirdi.

**Karar.** Varlık `TaskItem`, durum `TaskItemStatus`. Kullanıcıya her ikisi de
"Görev" olarak gösteriliyor.

**Gerekçe.** `docs/DATABASE.md` `TaskItem` adını zaten aynı gerekçeyle
belirlemişti; `TaskItemStatus` bunun doğal devamı. Alternatif olan takma adlar
her yeni dosyada hatırlanmak zorunda olurdu ve unutulduğunda hata mesajı
sorunun kendisini değil sonucunu gösterirdi.

**Sonuçlar.** Tel üzerindeki biçim etkilenmiyor: enum'lar üye adlarıyla
taşınıyor (ADR-0023), dolayısıyla frontend tarafında tip hâlâ `TaskStatus` ve
değerler `Todo` / `InProgress` / `Done`. `docs/PRODUCT.md` ve
`docs/DATABASE.md` bu adlandırmayı yansıtacak şekilde güncellendi.

---

## ADR-0030 — Görevde durum makinesi ve iyimser eşzamanlılık yok

**Bağlam.** `Ticket` hem geçiş tablosuyla hem `xmin` eşzamanlılık belirteciyle
korunuyor (ADR-0013). Aynı korumaların göreve de uygulanıp uygulanmayacağı
açıktı.

**Karar.** `TaskItem` üzerinde ne durum makinesi ne de eşzamanlılık belirteci
var. Üç durumun her biri diğerini izleyebiliyor ve yazma işlemleri istemciden
satır sürümü istemiyor.

**Gerekçe.** Talep müşteriye bakan bir iş akışı; "kapalı"dan doğrudan
"işlemde"ye atlamak geçmişi bulanıklaştırır ve bunu birden fazla kişi izler.
Görev ise bir sahibi ve tarihi olan iç hatırlatma. Tamamlandı'dan geri almak
bir düzeltmedir; reddetmek yalnızca insanlara görevi silip yenisini açmayı
öğretir ve korunmak istenen geçmiş böylece tamamen kaybolur.

Eşzamanlılık belirteci de aynı sebeple gereksiz: kayıp güncellemeyi olası kılan
şey aynı kaydın birden fazla kişi tarafından eşzamanlı düzenlenmesi, bu da
görevde olmuyor. Belirteç eklemek, gerçekleşmeyen bir çakışma için her formda
sürüm taşımak demekti.

**Sonuçlar.** `CompletedAt` talebin `ResolvedAt`'ının tersine geri alındığında
temizleniyor. Talepte ilk çözüm tarihi korunuyor çünkü ilk denemenin süresi
raporlanan bir sayı; görevde ise tamamlanmamış bir işin tamamlanma tarihi
yoktur ve bayat bir tarih onu bu alanı gösteren her listede bitmiş gösterirdi.

`PATCH /tasks/{id}` bütün düzenlenebilir alanları değiştiriyor, dolayısıyla
`null` "yok" demek. Bu, kısmi gövdenin JSON'da çözemediği "alan yok mu, değeri
mi null" belirsizliğini ortadan kaldırıyor ve görevin düzenleme formu zaten bu
alanların hepsini tutuyor.

---

## ADR-0031 — Dashboard tek yanıt, önbelleksiz, grafiksiz

**Bağlam.** Dashboard altı ayrı rakam, bir dağılım ve iki kısa liste gösteriyor.
Üç ayrı karar gerekiyordu: kaç uç nokta, önbellek var mı, grafik kütüphanesi
var mı.

**Karar.**

1. **Tek uç nokta:** `GET /api/workspaces/{slug}/dashboard` her şeyi döndürüyor.
2. **Önbellek yok.** Redis Faz 15'te ve ancak gerçek bir ölçümden sonra.
3. **Grafik kütüphanesi yok.** Talep dağılımı CSS ile çizilen yığılmış bir çubuk.

**Gerekçe.**

Kutu başına uç nokta, sayfanın ilk saniyesini kendini yeniden dizerek
geçirmesine yol açardı ve her istek aynı üyelik doğrulamasını tekrarlardı.
Ayrıca tüm rakamların **tek bir ana** karşı okunması ancak tek istekte mümkün:
saat sorgu başına okunsaydı bir görev sayıda gecikmiş, yanındaki listede
gecikmemiş görünebilirdi.

Önbellek, elde olmayan bir hız sorununu çözmek için bayatlık sorunu eklemek
olurdu. Sorgular indeksli sayımlardan ibaret. Ölçüm geldiğinde ADR-0015
zaten anahtar şemasını ve geçersiz kılma davranışını tanımlıyor.

Toplamı bilinen beş kategori bir orandır ve oran tek çubukta okunur. Bunun için
bir grafik kütüphanesi eklemek yüzlerce kilobayt, ikinci bir render modeli ve
kendi erişilebilirlik yüzeyini getirirdi — tarayıcının zaten çizdiği bir resim
için. Çubuk `aria-hidden`; yanındaki lejant aynı bilgiyi metin olarak veriyor,
dolayısıyla hiçbir şey rengi görmeye bağlı değil.

**Sonuçlar.** "Açık talep" **bitmemiş** anlamına geliyor: `Open`, `InProgress`
ve `Waiting` toplamı. Yalnızca `Open` sayılsaydı kuyruk dolarken ekran sakin
görünürdü.

Üye sayımı sayfadaki tek elle kapsanan sorgu, çünkü `Membership` bilinçli
olarak global query filter dışında (ADR-0024). İzolasyon testi bu rakamı ayrıca
doğruluyor.

Dashboard'daki rakamlar ilgili listeye bağlanıyor ama **filtrelenmiş** listeye
değil: liste filtrelerini bileşen durumunda tutuyor, dolayısıyla sorgu dizesi
filtresiz bir sayfaya düşer ve bağlantının verdiği sözü sessizce bozardı.
Filtreleri URL'den okumak yapılmaya değer, ama listeye ait bir iş.

---

## ADR-0032 — Mesajlaşma topolojisi ve tüketici barındırma

**Bağlam.** İlk gerçek asenkron iş akışı için RabbitMQ ekleniyor. Dört soru
birlikte cevaplanmalıydı: kaç exchange, kuyrukları kim bildirir, tüketiciler
nerede koşar, broker düştüğünde ne olur.

**Karar.**

1. **Tek topic exchange** (`flowdesk.events`), mesaj tipi başına ayrı kuyruk.
2. **Kuyrukları tüketici bildirir**, ve bunu `StartAsync` içinde, host başlamış
   sayılmadan önce yapar.
3. **Tüketiciler yalnızca Worker'da koşar**; API yayınlar, tüketmez.
4. **Broker hazırlık kontrolünde `Degraded`**, `Unhealthy` değil.

**Gerekçe.**

Mesaj tipi başına exchange, her yeni tip için yayıncı ile tüketici arasında
koordinasyon gerektirirdi. Topic exchange'de tüketici bir desene bağlanır ve
yeni bir tip eklemek topolojide hiçbir değişiklik istemez. Buna karşılık
kuyruklar ayrıdır: paylaşılan tek kuyruk, yavaş bir tüketicinin arkasında her
türden mesajı bekletir ve zehirli bir mesajı onu hiç istememiş işleyicilerin de
sorunu yapar.

Aboneliğin `ExecuteAsync` yerine `StartAsync`'te kurulması bir test tarafından
zorunlu kılındı. `ExecuteAsync` arka planda çalışır ve host kendini ilk
`await`'te başlamış sayar; aynı anda ayağa kalkan bir yayıncı hiçbir kuyruk
bağlanmadan mesaj gönderebilir, topic exchange de dinleyicisi olmayan mesajı
sessizce düşürür. `StartAsync`'te bildirmek, host başlamadan topolojinin var
olmasını garanti ediyor.

Bunun sonucu, broker başlangıçta erişilemezse worker'ın hiç başlamamasıdır. Tek
işi tüketmek olan bir süreç için doğru başarısızlık budur: yüksek sesle çökmek
yeniden başlatılmasını sağlar, başarıyla başlayıp hiçbir şey tüketmemek ise
bakan herkese sağlıklı görünür.

API'nin tüketmemesi, aynı mesajın iki kez işlenmemesi içindir. İki host da aynı
kuyrukları okusaydı her mesaj iki kez ele alınır ve ikinci ele alış API'nin
kendi loglarında görünmezdi.

`Degraded` ile `Unhealthy` ayrımı en önemli karar. API broker olmadan her
okumayı ve her yazmayı yapabiliyor; yalnızca ardından gidecek mesajlar
gecikiyor. Hazırlık kontrolünü düşürmek, hiçbir örneğin isteğe cevap vermek
için ihtiyaç duymadığı bir bağımlılık yüzünden bütün sağlam örnekleri aynı anda
rotasyondan çıkarır — ve e-posta kuyruğa alınamadığı için bütün ürünü
durdururdu. PostgreSQL ise zorunludur ve `Unhealthy` döndürür.

**Sonuçlar.** Teslimat **en az bir kez**. Broker, işlendiğinden emin olmadığı
her şeyi yeniden gönderir; tüketici aynı mesajı iki kez görebilmeli ve bir kez
davranmalıdır. `IntegrationMessage.MessageId` bunun içindir. Faz 11 bunu
`ProcessedMessage` tablosuyla otomatikleştirecek; o zamana kadar her tüketici
kendi idempotency'sinden sorumlu.

Yayınlama veritabanı transaction'ıyla atomik **değil**. Geri alınan bir
transaction'ın içinden gönderilen mesaj hiç olmamış bir şeyi anlatır; commit
sonrası gönderilen ise süreç arada ölürse kaybolur. Bu boşluğu Faz 11'deki
outbox kapatacak.

Okunamayan gövde yeniden kuyruğa **alınmıyor**: sonsuza dek aynı şekilde
başarısız olur ve prefetch bir olduğu için arkasındaki her mesajı tıkardı.
İşleyici hatası ise yeniden kuyruğa alınıyor, çünkü geçici olabilir. Dead-letter
kuyruğu ve yeniden deneme politikası Faz 11'e ait.

---

## ADR-0033 — Outbox: mesaj ile değişiklik aynı transaction'da

**Bağlam.** ADR-0032 bir boşluk bırakmıştı: yayınlama veritabanı
transaction'ıyla atomik değil. İki başarısızlık da gerçek:

- Transaction'ın **içinden** gönderilen mesaj, transaction geri alınırsa hiç
  olmamış bir şeyi anlatır. Tüketici var olmayan bir talebe e-posta gönderir.
- Commit **sonrasında** gönderilen mesaj, süreç arada ölürse kaybolur.
  Değişiklik olmuş ama kimse haberdar edilmemiştir.

İki fiili tek bir atomik işlemde birleştirmenin yolu yok, çünkü biri
veritabanında biri broker'da.

**Karar.**

1. Use case mesajı **kuyruğa alır**: kendi transaction'ına bir `OutboxMessage`
   satırı yazar. `IMessagePublisher` artık bunu yapıyor.
2. Worker'daki bir işleyici satırları broker'a taşıyor. Bu iş
   `IBrokerPublisher` üzerinden yapılıyor ve yalnızca işleyici çağırıyor.
3. Tüketici tarafında `ProcessedMessage` tablosu, aynı mesajın iki kez etki
   etmesini engelliyor.

**Gerekçe.**

Satır yazmak boşluğu kapatıyor çünkü satır ile değişiklik aynı transaction'da:
ikisi birlikte iner ya da hiçbiri inmez. Broker'a ulaşmak artık ayrı bir adım
ve başarısız olursa satır yerinde duruyor — kaybolmuyor, yeniden deneniyor.

**İki ayrı sözleşme**, çünkü ikisi farklı şey söylüyor. `IMessagePublisher`
"bunu kuyruğa al" demek ve Application'a ait; `IBrokerPublisher` "bunu şimdi
gönder" demek ve Infrastructure'a ait. Çağrı yerinde ikisi de aynı göründüğü
için — her ikisi de derleniyor — kayıtların doğruluğu testle sabitlendi.

**Yönlendirme anahtarı satırda saklanıyor**, tipten türetilmiyor. Türetmek,
adları tiplere geri eşleyen ve elle güncel tutulan bir kayıt defteri gerektirir;
anahtar satırdayken işleyici tek bir mesaj tipini bilmek zorunda kalmadan
yalnızca bayt taşıyor.

**Partiyi alma ile zamanlama ayrı sınıflarda.** Bir turun kuralları var —
satırlar nasıl kilitlenir, hata ne yapar, ne kadar beklenir; döngünün yalnızca
temposu var. Ayırmak, bir turun tek tek koşturulabilmesini sağlıyor ve her
doğrulamayı bir zamanlayıcıyla yarışa sokmaktan kurtarıyor.

**`FOR UPDATE SKIP LOCKED`**, çünkü birden fazla worker aynı anda çalışabilmeli.
Beklemek onları sıraya sokar ve ikincisini anlamsız kılardı; atlamak farklı
satırlar almalarını sağlıyor.

**Idempotency host'ta, tüketicide değil.** Her tüketicinin hatırlaması gereken
bir kural, birinin er geç unutacağı kuraldır ve belirtisi kimsenin bir müşteri
şikâyet edene kadar fark etmediği tekrarlanmış bir yan etkidir.

`ProcessedMessages` anahtarı mesaj **ve** tüketici. İki tüketici aynı mesaja
haklı olarak farklı tepki verebilir — biri e-posta gönderir, diğeri bildirim
yazar — ve yalnızca mesaja konan bir anahtar, önce bitenin diğerini sessizce
bastırmasına yol açardı.

Okuma bir hızlandırma, garanti değil. Yarışan iki teslimat da boş bulabilir ve
ikisi de çalışabilir; ikisinin birden etki etmesini engelleyen şey birincil
anahtardır: ikinci kayıt takılır ve kendi transaction'ını tüketicinin
yazdıklarıyla birlikte geri alır. Yani tam olarak bir tane veritabanı
değişikliği commit edilir.

**Sonuçlar.**

Bu, **veritabanı** değişikliklerini koruyor. Veritabanı dışındaki etkiler —
posta sunucusuna teslim edilmiş bir e-posta — geri alınamaz; dışa dönük yan
etkisi olan bir tüketici bunu kendisi kaydetmek zorunda, transaction onun
yerine yapamaz.

`OutboxMessage` ve `ProcessedMessage` bilinçli olarak `ITenantOwned` **değil**.
İşleyici ve tüketiciler istek dışında, kiracı bağlamı olmadan çalışıyor; global
query filter'a dâhil olsalardı hiçbir şey bulamazlardı (ADR-0024). Kiracı
kimliği mesajın gövdesinde taşınıyor.

`FlowDeskDbContext` kaydı kiracı bağlamını isteğe bağlı çözüyor, böylece tek
kayıt iki host'a birden hizmet ediyor: API'de filtreli, Worker'da etkisiz.
Worker'da etkisiz olması güvenli, çünkü Worker hiçbir kullanıcı isteğine cevap
vermiyor ve yazdığı her şey işlediği mesajın taşıdığı `TenantId` ile kapsanıyor.

Bekleyen satırlar için **kısmi indeks** (`WHERE ProcessedAt IS NULL`). Tablo
neredeyse tamamen işlenmiş geçmişten oluşuyor ve sorgu yalnızca baştaki birkaç
satırı istiyor; tamamını kapsayan bir indeks sınırsız büyürdü.

Dead-letter kuyruğu hâlâ yok. Okunamayan gövde yeniden kuyruğa alınmıyor ve
sonsuza dek başarısız olan bir outbox satırı üst sınıra kadar geri çekilip
orada kalıyor — `LastError` sütunuyla görünür hâlde. Otomatik bir çöp kutusu
eklemek, henüz görülmemiş bir başarısızlık biçimine karşı politika yazmak
olurdu.
