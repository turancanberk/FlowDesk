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
