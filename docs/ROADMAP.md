# FlowDesk — Yol Haritası

Fazların güncel durumu `docs/PROGRESS.md` içindedir. Bu doküman kapsamı ve
sırayı tanımlar.

## Faz planı

| Faz | Ad | Kapsam |
|---|---|---|
| 00 | Bootstrap | Depo iskeleti, dokümantasyon, ADR'ler, Git/GitHub kurulumu |
| 01 | Temel | .NET 10 solution, Next.js, Docker Compose (PostgreSQL), sağlık uç noktaları |
| 02 | Tasarım Sistemi | Token'lar, bileşenler, `/design-system` vitrini |
| 03 | Kimlik Doğrulama | Identity, access/refresh token, rotation, replay tespiti |
| 04 | Çok Kiracılılık | Tenant, Membership, workspace, izolasyon testleri |
| 05 | Ekip ve Davetler | Roller, izin matrisi, davet yaşam döngüsü |
| 06 | Müşteriler | CRUD, arama, filtre, sıralama, sayfalama, detay |
| 07 | Talepler | CRUD, atama, yorum, durum geçişleri, eşzamanlılık |
| 08 | Görevler | CRUD, atama, son tarih, durum, müşteri ilişkisi |
| 09 | Dashboard | Operasyonel toplamlar |
| 10 | RabbitMQ ve Worker | Mesaj altyapısı, Worker barındırma |
| 11 | Outbox | Transaction içi olay kaydı, güvenilir yayın |
| 12 | E-posta ve Bildirimler | Mailpit, davet e-postası, bildirimler |
| 13 | Dosya Ekleri | Azurite, güvenli yükleme ve indirme |
| 14 | Denetim ve Etkinlik | ActivityEvent, Türkçe etkinlik akışı |
| 15 | Redis Önbellek | Dashboard önbelleği, TTL ve geçersiz kılma |
| 16 | Gelişmiş Entegrasyon Testleri | İzolasyon, yetki, yaşam döngüsü senaryoları |
| 17 | Playwright E2E | Deterministik tarayıcı yolculukları |
| 18 | Gözlemlenebilirlik | Serilog, OpenTelemetry, Prometheus, Grafana |
| 19 | Güvenlik Sertleştirme | Başlıklar, rate limiting, bağımlılık taraması |
| 20 | CI/CD | GitHub Actions, üretim Dockerfile'ları, Caddy |
| 21 | Demo Verisi | Gerçekçi Türkçe seed verisi |
| 22 | README ve Portfolyo | Diyagram, ekran görüntüleri, kurulum dokümanı |
| 23 | Nihai Denetim | Baştan sona doğrulama |

## Kapsam dışı

Aşağıdakiler çekirdek kapsam tamamlanmadan uygulanmaz. Her biri için gerekçe
ve olası gelecek yaklaşımı aşağıdadır.

### Koyu tema

Token yapısı ikinci bir temaya izin verecek şekilde kurulur, ancak koyu tema
uygulanmaz. Gerekçe: her bileşenin iki temada doğrulanması gerekir ve bu,
çekirdek işlevselliğin önüne geçer. İleride `:root[data-theme="dark"]` altında
token'lar yeniden tanımlanarak eklenebilir.

### Google OAuth

E-posta/parola ile kimlik doğrulama ürünün ihtiyacını karşılıyor. OAuth,
harici bir hesap yapılandırması ve yönlendirme URI yönetimi gerektirir.
ASP.NET Core Identity harici sağlayıcıları desteklediği için ileride eklenmesi
mekanik bir iştir.

### Wildcard workspace subdomain

ADR-0005 gereği yol tabanlı rotalama seçildi. Subdomain, wildcard DNS ve
wildcard TLS sertifikası gerektirir; bu, sahip olunan bir alan adına bağlıdır.

### Kanban görünümü

Görevler önce liste olarak modellenir. Kanban bir görselleştirme katmanıdır ve
temel görev yaşam döngüsü oturmadan eklenmesi anlamsızdır.

### WebSocket ve gerçek zamanlı varlık

Mevcut akışlar istek/yanıt ile iyi çalışıyor. Gerçek zamanlı güncelleme
kalıcı bağlantı yönetimi, ölçekleme ve yeniden bağlanma mantığı getirir;
karşılığında bu ölçekte somut bir fayda üretmez.

### Faturalandırma ve Stripe

Ürün henüz ticari bir akış içermiyor. Harici hesap ve gerçek ödeme kimlik
bilgisi gerektirir.

### Kubernetes ve mikroservisler

ADR-0001 gereği bilinçli olarak dışarıda. Dağıtım Caddy + Docker Compose ile
yapılır.

### Mobil uygulama

Ürün masaüstü öncelikli bir operasyon aracıdır. Web arayüzü mobilde kullanışlı
biçimde çalışır; ayrı bir uygulama ayrı bir ürün kapsamıdır.

### Özelleştirilebilir iş akışları

Talep durumları sabit bir kümedir. Kiracı başına özelleştirilebilir durum
makinesi, domain invariantlarını çalışma zamanı verisine taşır ve doğrulamayı
önemli ölçüde karmaşıklaştırır. Gerçek bir kullanıcı talebi oluşmadan
eklenmez.

## Faz 13 sonrası bilinerek bırakılan

- **Yüklenen dosyalarda virüs taraması.** Gerçek bir üründe gerekli olurdu;
  burada kapsam dışı. Beyaz liste ve özel konteyner, taramanın yerini tutmaz —
  yalnızca saldırı yüzeyini daraltır (ADR-0035).
- **İmzalı depolama bağlantısı (SAS).** Bilinçli olarak kullanılmıyor: dosya
  trafiğini API'den çıkarırdı ama kiracı izolasyonunu adresi bilen herkesin
  eline bırakırdı.

## Faz 14 sonrası bilinerek bırakılan

- **Etkinlik saklama süresi (retention).** `ActivityEvents` tablosu sınırsız
  büyüyor. Gerçek bir dağıtımda kesme veya arşivleme politikası gerekirdi;
  burada kapsam dışı (ADR-0036).
