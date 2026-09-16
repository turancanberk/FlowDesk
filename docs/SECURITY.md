# FlowDesk — Güvenlik

Güvenlik bir bitirme işi değil, her fazın parçasıdır. Bu doküman uygulanan
modeli ve gerekçelerini tanımlar.

## Temel ilkeler

- **Yetkilendirme backend'de zorunlu kılınır.** Frontend'de bir düğmenin
  gizlenmesi güvenlik sınırı değildir; yalnızca kullanıcı deneyimidir.
- **Kiracı izolasyonu bir güvenlik sınırıdır.** Kolaylık gerekçesiyle
  gevşetilmez.
- **Belirsizlikte reddet.** Yetkinin kesin olmadığı yerde erişim verilmez.
- **En az yetki.** Her rol yalnızca işini yapacak kadar yetki alır.
- **Çerçeve korumalarını kullan.** Kriptografi yeniden icat edilmez.

---

## 1. Kimlik doğrulama

ASP.NET Core Identity kullanılır. Parola hash'leme, kilitleme ve doğrulama
çerçevenin kendi mekanizmalarıyla yapılır.

### Access token

| Özellik | Değer |
|---|---|
| Tür | JWT |
| Ömür | Kısa (~10 dakika) |
| Saklama yeri | **Yalnızca tarayıcı belleği** (modül kapsamlı auth store) |
| Taşıma | `Authorization: Bearer <token>` başlığı |

`localStorage`, `sessionStorage` veya herhangi bir kalıcı tarayıcı deposu
**kullanılmaz**. Gerekçe: bu depolar JavaScript'e açıktır ve bir XSS açığında
token kalıcı olarak sızdırılabilir. Bellekte tutulan token sayfa yenilenmesiyle
kaybolur ve refresh akışıyla yeniden alınır.

Kolaylık gerekçesiyle `localStorage` yaklaşımına dönülmez.

### Refresh token

| Özellik | Değer |
|---|---|
| Ömür | Uzun (14 gün) |
| Saklama yeri (istemci) | `HttpOnly` + `Secure` çerez |
| `SameSite` | `Strict` |
| `Path` | Yalnızca refresh/logout uç noktaları |
| Saklama yeri (sunucu) | Ham değer değil, **SHA-256 hash'i** |
| Döngü | Her kullanımda döner (rotating) |

#### Rotation ve replay tespiti

Her oturum bir **token family** (aile) kimliği taşır. Refresh sırasında:

1. Sunulan token'ın hash'i aranır.
2. Bulunamazsa istek reddedilir.
3. Bulunan token daha önce kullanılmışsa bu bir **replay göstergesidir**:
   ailenin tamamı iptal edilir ve istek reddedilir. Çalınmış bir token'ın ikinci
   kullanımı oturumu düşürür.
4. Geçerliyse eski token kullanılmış olarak işaretlenir, aynı aile içinde yeni
   bir token üretilir ve çereze yazılır.

`Logout` işlemi refresh token ailesini sunucu tarafında geçersizleştirir ve
çerezi temizler.

#### Access token iptali

Access token için kara liste tutulmaz. Bu bilinçli bir ödünleşimdir: kara liste
her istekte bir merkezi kontrol gerektirir ve JWT'nin durumsuzluk avantajını
ortadan kaldırır. Bunun yerine access token ömrü kısa tutulur; iptal edilen bir
oturum en fazla bir access token ömrü kadar geçerli kalır.

### Oturum yeniden kurulumu

Uygulama açıldığında bellekte access token yoktur. Frontend sessizce
`POST /api/auth/refresh` çağırır. `401` alınan isteklerde tek uçuşlu
(single-flight) yenileme yapılır ve istek bir kez tekrarlanır; yenileme
başarısız olursa giriş ekranına yönlendirilir.

---

## 2. CSRF

Access token **başlıkla** taşındığı ve bellekte tutulduğu için normal API
çağrılarında CSRF yüzeyi yoktur: tarayıcı bu başlığı otomatik eklemez ve
saldırganın sitesi token'ı okuyamaz.

Geriye kalan CSRF yüzeyi yalnızca çerez taşıyan `refresh` ve `logout` uç
noktalarıdır. Dört savunma katmanı uygulanır:

1. **`SameSite=Strict`** — siteler arası istekte çerez hiç gönderilmez.
   Birincil savunma budur.
2. **Dar `Path` kapsamı** — çerez yalnızca auth uç noktalarına gider, diğer
   isteklerde ağda dolaşmaz.
3. **Zorunlu özel istek başlığı** — siteler arası çağrıda CORS preflight'ı
   tetikler; basit form tabanlı CSRF denemelerini keser.
4. **Rotation + replay tespiti** — başarılı bir CSRF bile tek kullanımlık
   kalır ve ikinci kullanımda aile düşer.

---

## 3. CORS

- Geliştirmede yalnızca bilinen frontend origin'ine (`http://localhost:3000`)
  ve `AllowCredentials` ile izin verilir.
- Joker origin (`*`) **kullanılmaz**. Kimlik bilgisi taşıyan isteklerde zaten
  geçersizdir ve yanlış güvenlik hissi yaratır.
- Üretimde Caddy, frontend ve API'yi aynı origin altında sunar. Bu durumda
  istekler same-origin olur ve CORS'a ihtiyaç kalmaz.

---

## 4. Çerez ve HTTPS

| Bayrak | Değer | Gerekçe |
|---|---|---|
| `HttpOnly` | açık | JavaScript çerezi okuyamaz; XSS ile çalınamaz |
| `Secure` | açık | Yalnızca HTTPS üzerinden gönderilir |
| `SameSite` | `Strict` | Siteler arası istekte gönderilmez |
| `Path` | dar | Yalnızca auth uç noktaları |

Çerez güvenlik politikası `Auth__Cookies__SecurePolicy` ile yapılandırılır.
Varsayılan `Always`'tir ve **üretimde gevşetilemez**. Yerel geliştirmede
tarayıcılar `http://localhost` için `Secure` çerezleri kabul eder.

---

## 5. Yetkilendirme ve izin matrisi

Roller `Membership` üzerinde tutulur. İzin mantığı tek bir merkezde tanımlanır
ve ASP.NET Core authorization policy'leri üzerinden uygulanır. Uç noktalarda
dağınık `if (role == ...)` kontrolleri yazılmaz.

| İşlem | Owner | Admin | Agent | Viewer |
|---|:---:|:---:|:---:|:---:|
| Workspace'i görüntüle | ✓ | ✓ | ✓ | ✓ |
| Workspace ayarlarını düzenle | ✓ | ✓ | — | — |
| Workspace'i sil | ✓ | — | — | — |
| Sahipliği devret | ✓ | — | — | — |
| Üyeleri listele | ✓ | ✓ | ✓ | ✓ |
| Üye davet et | ✓ | ✓ | — | — |
| Üye rolünü değiştir | ✓ | ✓ | — | — |
| Üyeyi çıkar | ✓ | ✓ | — | — |
| Müşteri görüntüle | ✓ | ✓ | ✓ | ✓ |
| Müşteri oluştur / düzenle | ✓ | ✓ | ✓ | — |
| Müşteri arşivle | ✓ | ✓ | — | — |
| Talep görüntüle | ✓ | ✓ | ✓ | ✓ |
| Talep oluştur / düzenle | ✓ | ✓ | ✓ | — |
| Talep ata | ✓ | ✓ | ✓ | — |
| Talep durumu değiştir | ✓ | ✓ | ✓ | — |
| Talep yorumu ekle | ✓ | ✓ | ✓ | — |
| Talep sil | ✓ | ✓ | — | — |
| Görev görüntüle | ✓ | ✓ | ✓ | ✓ |
| Görev oluştur / düzenle | ✓ | ✓ | ✓ | — |
| Görev sil | ✓ | ✓ | — | — |
| Ek dosya yükle | ✓ | ✓ | ✓ | — |
| Ek dosya indir | ✓ | ✓ | ✓ | ✓ |
| Ek dosya sil | ✓ | ✓ | — | — |
| Etkinlik geçmişini görüntüle | ✓ | ✓ | ✓ | ✓ |

Kurallar:

- Bir Owner kendini son Owner olduğu workspace'ten çıkaramaz ve rolünü
  düşüremez. Önce sahiplik devredilmelidir.
- Admin, Owner'ın rolünü değiştiremez ve Owner'ı çıkaramaz.
- Viewer hiçbir yazma işlemi yapamaz.

---

## 6. Kiracı izolasyonu

Üç savunma katmanı birlikte çalışır. Hiçbiri tek başına yeterli sayılmaz.

### Katman 1 — Üyelik doğrulaması (birincil)

Her tenant kapsamlı istekte, `workspaceSlug` ile kimliği doğrulanmış kullanıcı
arasındaki `Membership` kaydı doğrulanır. Üyelik yoksa istek reddedilir.
İstemciden gelen workspace kimliği hiçbir zaman doğrulanmadan kabul edilmez.

### Katman 2 — EF Core global query filter

Kiracıya ait tüm varlıklar, geçerli `TenantId` üzerinden global query filter ile
süzülür. Bu bir güvenlik ağıdır; unutulan bir `Where` yüzünden veri sızmasını
engeller. **Tek başına güvenlik sınırı sayılmaz**, çünkü filtre
`IgnoreQueryFilters` ile atlanabilir ve ham SQL'i kapsamaz.

### Katman 3 — Yazma tarafı sahiplik kontrolü

Bir kaynak başka bir kaynağa bağlanırken (örneğin talebe müşteri atanırken),
hedefin aynı tenant'a ait olduğu açıkça doğrulanır. Yabancı kiracı kaynağı
hiçbir koşulda mevcut kiracının varlığına iliştirilemez.

### Yanıt kodu politikası

Yabancı kiracıya ait bir kaynak istendiğinde **`404 Not Found`** döndürülür,
`403 Forbidden` değil. Gerekçe: `403`, kaynağın var olduğunu doğrulayarak
numaralandırma (enumeration) yoluyla bilgi sızdırır. `404` böyle bir bilgi
vermez.

### Zorunlu testler

Aşağıdaki senaryolar için entegrasyon testi yazılması zorunludur. Bu testler
hiçbir koşulda silinmez, `Skip` edilmez veya assertion'ları zayıflatılmaz.

- Tenant A, Tenant B müşterisini **okuyamaz**
- Tenant A, Tenant B müşterisini **güncelleyemez**
- Tenant A, Tenant B müşterisini **silemez**
- Tenant A, Tenant B talebine **erişemez**
- Tenant A, Tenant B görevine **erişemez**
- Tenant A, Tenant B ekini **indiremez**
- Tenant A, Tenant B üyelerini **listeleyemez**
- Tenant A, kendi talebine Tenant B müşterisini **bağlayamaz**

---

## 7. Davet güvenliği

- Davet token'ı kriptografik olarak güvenli rastgele üretilir.
- Veritabanında **ham değer saklanmaz**, hash'i saklanır.
- Davetin son kullanma tarihi vardır; süresi dolmuş davet kabul edilemez.
- Davet **tek kullanımlıktır**; kabul edilmiş bir davet ikinci kez kabul
  edilemez.
- Davetin kabulü, davet edilen e-posta adresiyle eşleşme koşuluna bağlıdır.
- Davet kabul uç noktası rate limiting kapsamındadır.

---

## 8. Girdi doğrulama ve overposting

- Sunucu tarafı doğrulama zorunludur. İstemci doğrulaması yalnızca kullanıcı
  deneyimi içindir ve backend doğrulamasının yerine geçmez.
- EF Core varlıkları **API sözleşmesi olarak dışa verilmez**. Açık request ve
  response DTO'ları kullanılır.
- Request modelleri yalnızca istemcinin kontrol etmesine izin verilen alanları
  içerir. `TenantId`, `CreatedByUserId`, `CreatedAt` gibi alanlar istemciden
  alınmaz; sunucuda belirlenir.

---

## 9. Dosya yükleme

- Maksimum dosya boyutu sınırı uygulanır.
- İçerik tipi beyaz liste ile doğrulanır; istemcinin bildirdiği `Content-Type`
  tek başına güvenilmez.
- Dosya adı temizlenir; yol ayracı ve üst dizin ifadeleri (`..`) reddedilir.
- Depolama anahtarı sunucuda üretilir ve tenant kimliğini içerir; istemciden
  gelen ad depolama yolunu belirlemez.
- İndirme işlemi tenant sahipliği ve üyelik doğrulamasından geçer.

---

## 10. Rate limiting

ASP.NET Core rate limiting kullanılır. Kapsanan uç noktalar:

| Uç nokta | Gerekçe |
|---|---|
| `POST /api/auth/login` | Parola deneme (brute force) saldırısı |
| `POST /api/auth/register` | Toplu hesap oluşturma |
| `POST /api/auth/refresh` | Token deneme ve kötüye kullanım |
| Davet kabul | Token tahmin denemesi |

Politika ayrıntıları Faz 19'da kesinleştirilir ve burada güncellenir.

---

## 11. Hata biçimi ve bilgi sızıntısı

- Tüm hatalar ASP.NET Core `ProblemDetails` biçiminde döner.
- Üretim yanıtlarında **stack trace bulunmaz**.
- İç istisna ayrıntıları son kullanıcıya gösterilmez; yapılandırılmış log'a
  yazılır.
- Kullanıcıya gösterilen hata metinleri Türkçe'dir.
- Kimlik doğrulama hatalarında "kullanıcı yok" ile "parola yanlış" ayrımı
  yapılmaz; tek bir genel mesaj döner.

## 12. Log hijyeni

Log kayıtlarına şunlar **yazılmaz**: parola, access token, refresh token, davet
token'ı, çerez içeriği, `Authorization` başlığı, bağlantı dizesi.

Yapılandırılmış log'da bağlam için kullanıcı kimliği ve tenant kimliği
tutulabilir; e-posta ve kişisel veri gereksizce loglanmaz.

---

## 13. Gizli bilgi yönetimi

Bu depo **public**'tir.

Hiçbir koşulda commit edilmeyecekler: gerçek `.env`, veritabanı parolası, JWT
imzalama anahtarı, refresh token, GitHub token, cloud API anahtarı, SMTP
kimlik bilgisi, Azure kimlik bilgisi, private key, sertifika özel materyali,
kişisel veri.

Bunun yerine kullanılır:

- `.env.example` — yalnızca örnek/dummy değerler
- ortam değişkenleri
- .NET user-secrets (yerel geliştirme)
- GitHub Secrets (CI/CD)

Her push öncesi `git diff` ve staged/untracked dosyalar bu açıdan denetlenir.

---

## 14. Güvenlik başlıkları

Faz 19'da uygulanacaklar: `Content-Security-Policy`,
`Strict-Transport-Security`, `X-Content-Type-Options: nosniff`,
`Referrer-Policy`, `X-Frame-Options` / `frame-ancestors`, `Permissions-Policy`.

## 15. Bağımlılık güvenliği

Her güvenlik incelemesinde çalıştırılır:

```bash
dotnet list backend/FlowDesk.slnx package --vulnerable --include-transitive
npm --prefix frontend audit
```

Bilinen ciddi açığı olan sürümler kullanılmaz.
