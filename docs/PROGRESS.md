# FlowDesk — İlerleme

Bu dosya faz seviyesindeki ilerlemeyi takip eder. Her faz sonunda güncellenir.
Operasyonel devir ayrıntısı için `docs/HANDOFF.md`.

**Son güncelleme:** 2026-09-17

---

## Fazlar

- [x] Faz 00 — Bootstrap
- [x] Faz 01 — Temel
- [x] Faz 02 — Tasarım Sistemi
- [x] Faz 03 — Kimlik Doğrulama
- [x] Faz 04 — Çok Kiracılılık
- [x] Faz 05 — Ekip ve Davetler
- [x] Faz 06 — Müşteriler
- [x] Faz 07 — Talepler
- [x] Faz 08 — Görevler
- [x] Faz 09 — Dashboard
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

**Faz 10 — RabbitMQ ve Worker**

Durum: Başlanmadı

---

## Faz geçmişi

### Faz 09 — Dashboard · Tamamlandı

Tek uç nokta, tek use case: `GET /api/workspaces/{slug}/dashboard` (ADR-0031).

- Altı rakam: müşteri, açık talep, atanmamış talep, geciken görev, bu hafta
  biten görev, ekip üyesi.
- Talep durum dağılımı, enum sırasında — sayıya göre sıralamak çubukları her
  yenilemede yerinden oynatırdı.
- Son hareket eden beş talep ve en yakın beş görev.
- Bütün rakamlar **tek bir ana** karşı okunuyor; saat sorgu başına okunsaydı bir
  görev sayıda gecikmiş, yanındaki listede gecikmemiş görünebilirdi.
- "Açık talep" bitmemiş demek: `Open + InProgress + Waiting`. Yalnızca `Open`
  sayılsaydı ekran kuyruk dolarken sakin görünürdü.
- Üye sayımı sayfadaki tek elle kapsanan sorgu; `Membership` bilinçli olarak
  global query filter dışında (ADR-0024) ve eksik bir koşul başka bir kuruluşun
  mevcudunu raporlardı. İzolasyon testi bunu ayrıca doğruluyor.
- **Önbellek yok.** Redis Faz 15'te ve ancak gerçek bir ölçümden sonra.
- **Grafik kütüphanesi yok.** Dağılım CSS ile çizilen yığılmış bir çubuk;
  `aria-hidden` ve yanındaki lejant aynı bilgiyi metin olarak veriyor.
- Etkinlik akışı bu fazda **yok**. `ActivityEvent` Faz 14'e ait; uydurma bir
  akış göstermek yerine son hareket eden talepler ve yaklaşan görevler
  gösteriliyor — ikisi de gerçek veri.

Arayüz: altı rakam ayrı kartlar yerine tek şeritte, ince çizgilerle bölünmüş.
Kartlar sayıları birbirinden uzaklaştırıp bir bakışı taramaya çevirirdi; bu
rakamlar birlikte, çalışma alanı hakkında tek bir cümle olarak okunuyor.

Testler: 386/386 (201 birim + 185 entegrasyon).

---

### Faz 08 — Görevler · Tamamlandı

Domain:

- `TaskItem` ve `TaskItemStatus` adlandırması `System.Threading.Tasks` ile
  çakışmayı önlüyor (ADR-0029). Çakışma gizlenmiyor, belirsizlik üretip
  derlemeyi kırardı.
- Durum makinesi **yok** (ADR-0030). Tamamlandı'dan geri almak bir düzeltmedir;
  reddetmek insanlara görevi silip yenisini açmayı öğretir ve korunmak istenen
  geçmiş tamamen kaybolur.
- `CompletedAt` geri alındığında temizleniyor — talebin `ResolvedAt`'ının
  tersine. Tamamlanmamış işin tamamlanma tarihi yoktur.
- Son tarih isteğe bağlı. Zorunlu tarih insanlara tarih uydurtur, uydurulmuş
  son tarih geciken listesini güvenilmez yapar, güvenilmeyen liste okunmaz.
- `IsOverdue` saklanmıyor, sorgu anında hesaplanıyor; gecikme için gece işi
  gerekmiyor.

Infrastructure:

- Müşteri ve atanan kişi yabancı anahtarları `SetNull`. Kişi ayrıldığında
  "faturayı geri ara" görevi hâlâ yapılmalı; yalnızca kimsenin üzerinde
  olmadan duruyor.
- İndeksler: `(TenantId, Status)`, `(TenantId, DueAt)`, `(TenantId,
  AssignedUserId)`, `(TenantId, CustomerId)`.

Application:

- Altı use case: Create, Update, ChangeStatus, Delete, Get, List.
- `PATCH` bütün düzenlenebilir alanları değiştiriyor, dolayısıyla `null` "yok"
  demek. Kısmi gövdenin JSON'da çözemediği belirsizlik böylece ortadan kalkıyor.
- Durum değişikliği ayrı rotada; listedeki onay kutusunun tek istek olması için.
- İzin matrisi üç eylemle genişledi: `ViewTasks` (İzleyici), `ManageTasks`
  (Temsilci), `DeleteTasks` (**Temsilci** — görev iç iştir, talebin aksine
  dışarıdan kimse ona atıfta bulunmaz).
- Varsayılan sıralama en yakın son tarih önce, tarihsiz işler en sonda.

Frontend:

- Tablo değil satır listesi; satır başındaki onay kutusu tamamlamayı tek tık
  yapıyor.
- Gecikme kendi rozetiyle gösteriliyor, kırmızı tarih metniyle değil.
- Son tarih girdisi seçilen günün sonuna ve kullanıcının saat dilimine
  sabitleniyor.
- Görev listesi müşteri detay sekmesiyle paylaşılıyor.

Testler: 377/377 (201 birim + 176 entegrasyon).

Bu faz sırasında yakalanan gerçek hata, aslında Faz 07'ye aitti:

**Bir çalışma alanındaki ilk eşzamanlı taleplerde sayaç çakışması.** Tam çözüm
koşusunda ara sıra tek bir test düşüyordu. İlk seferinde çıktı kırpıldığı için
hangi test olduğu kaybedildi; ikinci düşüşte çıktı saklanınca sebep görüldü.

`FOR UPDATE` yalnızca var olan bir satırı kilitleyebiliyor. Sayaç ilk kullanımda
oluşturulduğu için, bir çalışma alanında açılan ilk iki talep eşzamanlı
geldiğinde ikisi de sayaç bulamıyor, ikisi de ekliyor ve ikincisi birincil
anahtar ihlaliyle `500` dönüyordu — tam olarak kilidin kapsaması gereken durum,
kilidin henüz tutunacak bir şey bulamadığı an.

`ON CONFLICT DO NOTHING` ile satırın varlığı garantilendi. Regresyon testi
düzeltme olmadan başarısız oluyor ve beş kez tekrarlıyor, çünkü iki isteğin
gerçekten örtüşüp örtüşmediği zamanlama meselesi.

---

### Faz 07 — Talepler · Tamamlandı

Domain:

- `Ticket` kendi durum geçişlerini koruyor. Geçiş tablosu ortada bağışlayıcı
  (Açık, İşlemde ve Bekliyor arasında gerçek destek işi ileri geri hareket
  eder), uçta katı: kapalı bir talep yalnızca Açık'a dönebilir, böylece yeniden
  açma geçmişte görünür bir karar oluyor.
- `ResolvedAt` ilk çözümde yazılıyor ve sonraki çözümlerde korunuyor; ilk
  denemenin ne kadar sürdüğü silinmiyor.
- `TicketNumber` (`TLP-1042`) kiracı başına sıralı. Küresel numaralandırma bir
  kuruluşun kendi dizisindeki boşluklardan başkasının hacmini çıkarmasına izin
  verirdi.
- `TenantCounter`: veritabanı sequence'ı yerine satır. Sequence transactional
  değil; geri alınan bir oluşturma yine de numara tüketip görünür bir boşluk
  bırakırdı.
- `TicketComment`: ayrı varlık, `ITenantOwned`.

Infrastructure:

- `xmin` sistem sütunu eşzamanlılık belirteci (ADR-0013). Npgsql bunu sistem
  sütunu olarak tanıdığı için `CREATE TABLE` çıktısında yer almıyor; doğrulandı.
- Müşteri yabancı anahtarı `Restrict`. Müşteri silinmiyor arşivleniyor
  (ADR-0012) ve bunun sebebi zaten talep geçmişinin korunması; cascade tam da
  arşivlemenin var oluş sebebini yok ederdi.
- Yorum yabancı anahtarı `Cascade`: yorumun talebi olmadan anlamı yok.
- `TakeNextTicketNumberAsync` sayaç satırını `FOR UPDATE` ile kilitliyor ve
  talep eklemesiyle **aynı transaction** içinde çalışıyor.

Application:

- Dokuz use case: Create, Update, ChangeStatus, Assign, Delete, Get, List,
  AddComment, ListComments.
- `TicketGuards`: müşteri ve atanan kişi her yazmadan önce çalışma alanına ait
  mi diye doğrulanıyor. Query filter neyin *okunabileceğini* sınırlar; istek
  gövdesinde yabancı bir kimliğin gelmesini engellemez.
- İzin matrisi dört eylemle genişledi: `ViewTickets` (İzleyici),
  `ManageTickets` (Temsilci), `CommentOnTickets` (Temsilci),
  `DeleteTickets` (Yönetici).
- Arama numara gibi görünüyorsa numara olarak ele alınıyor: `1042`, `TLP-1042`
  ve `tlp-1042` aynı talebi buluyor.

API:

- Durum ve atama kendi eylem rotalarında (ADR-0027). `PATCH` yalnızca alan
  düzenlemesi yapıyor ve `version` istiyor (ADR-0028).

Frontend:

- Liste: debounce'lu arama, durum/öncelik/atama/sıralama filtreleri, sayfalama.
  "Bana atananlar" ve "atanmamışlar" tek kontrolde toplandı; kullanıcı için aynı
  karar.
- Detay: durum seçicisi yalnızca sunucunun gönderdiği geçişleri sunuyor, atama
  seçicisi üyelerden besleniyor, yorum akışı eskiden yeniye.
- Talep tablosu müşteri detay sekmesiyle paylaşılıyor; orada müşteri sütunu
  düşürülüyor.

Testler: 320/320 (169 birim + 151 entegrasyon). On eşzamanlı oluşturma on
farklı ve boşluksuz numara üretiyor; bu test sayaç tasarımının varlık sebebi ve
gerçek PostgreSQL üzerinde koşuyor.

Bu faz sırasında karşılaşılan iki gerçek durum:

1. **React Compiler uyarısı.** `react-hook-form`'un `watch()` fonksiyonu her
   render'da kimlik değiştirdiği için derleyici bileşenin tamamını optimizasyon
   dışı bırakıyordu. Uyarı bastırılmadı; `useWatch` kancasına geçildi.
2. **Boş dize sentinel.** Atanmamış seçeneği önce boş dize taşıyordu. Boş değer
   bir select için "hiçbir şey seçilmedi" demek ve kontrol "Atanmamış" yerine
   boş görünürdü; adlandırılmış sentinel'e geçildi.

---

### Faz 06 — Müşteriler · Tamamlandı

Domain:

- `Customer`, `ITenantOwned` uyguladığı için global query filter'a
  kendiliğinden kaydoldu. Filtre bu varlıkta arşiv koşulunu da taşıdığından
  adlandırılmış özel durum olarak yazıldı.
- Arşivlenen tek varlık (ADR-0012). Arşivli müşteri listeden çıkıyor ama detay
  sayfasından okunabiliyor; eski bağlantılar çalışmaya devam ediyor.
- `CustomerDetails` kaydı: oluşturma ve güncelleme aynı doğrulamadan geçiyor.
- `TurkishText.Fold`: ortak Türkçe harf katlaması; `WorkspaceSlug` de bunu
  kullanıyor.

Application:

- Altı use case: Create, Update, Archive, Restore, Get, List.
- `PagedResult` ve `PageRequest`: sayfa boyutu reddedilmiyor, kırpılıyor.
- Sıralama her zaman `Id` ile tie-break yapıyor.
- Arama katlanmış `SearchIndex` sütunu üzerinden (ADR-0025).

Frontend:

- TanStack Table v8 ile yoğun liste, debounce'lu arama, durum ve sıralama
  filtreleri, arşivlenenleri göster seçeneği.
- Detay sayfası: genel bakış, talepler ve görevler sekmeleri.
- Form diyaloğu oluşturma ve düzenleme için ortak; `key` ile yeniden bağlanıyor.
- Dört ayrı asenkron durum: yükleniyor, hiç kayıt yok, filtreye uyan yok, hata.

Testler: 241/241. Kiracı izolasyonu davranışsal olarak doğrulandı — iki
kiracının müşterileri aynı tabloda dururken her sorgu yalnızca kendi kiracısının
satırlarını görüyor, arşivli kayıtları istemek bile başka bir alana uzanmıyor.

Bu faz sırasında çözülen dört gerçek hata:

1. **Türkçe arama çalışmıyordu.** "YAZILIM" araması "Kuzey Yazılım" kaydını
   bulamıyordu; noktalı ve noktasız i hiçbir küçültme stratejisiyle
   eşleşmiyordu. Katlanmış `SearchIndex` sütunuyla çözüldü (ADR-0025).
2. **Mimari sınır sağlayıcı sızıntısı yakaladı.** `EF.Functions.ILike` Npgsql'e
   ait ve Application katmanı bilinçli olarak sağlayıcıya bağımlı değil.
3. **Select bileşenleri İngilizce kod gösteriyordu.** Filtre ve sıralama
   seçicilerinde "all" ve "RecentlyUpdated" görünüyordu. Base UI'ye `items`
   eşlemesi verilerek Türkçe etiketlere çevrildi.
4. **Bulut senkronizasyonu 10 yinelenmiş dosya üretip derlemeyi bozdu.**
   `scripts/clean-sync-duplicates.sh` eklendi.

### Faz 05 — Ekip ve Davetler · Tamamlandı

Domain:

- `Invitation`: hash'li token, süre dolumu (7 gün), tek kullanımlık kabul,
  iptal. Kabul, davet edilen adrese bağlı — bu kural entity'de duruyor, çünkü
  iletilmiş bir bağlantının onu alan kişi tarafından kullanılmasını engelleyen
  kural bu.
- Adres normalleştirmesi invariant küçültme ile; Türkçe kültür kuralı "I"
  harfini noktasız "ı"ya eşleyip daveti kendi alıcısına kapatırdı.

Application:

- İzin matrisi üye ve davet eylemleriyle genişletildi.
- Use case'ler: `ListMembers`, `ChangeMemberRole`, `RemoveMember`,
  `InviteMember`, `ListInvitations`, `RevokeInvitation`, `AcceptInvitation`.
- "Son sahip mi?" sorusu üç yerde geçiyor ve tek bir yerde soruluyor.
- Ekip listesi Identity hesaplarını tek sorguda alıyor.

Yetki kuralları:

- Admin ekibi yönetir ama üstündekileri yönetemez ve sahiplik dağıtamaz.
- Son sahip ne rolünü düşürebilir ne de ayrılabilir.
- Ayrılmak başkasını çıkarmakla aynı eylem değil; her üye ayrılabilir.
- Üyelik silindiğinde erişim aynı access token ile anında kesiliyor.

API:

- Kabul uç noktası çalışma alanı kapsamının dışında; kapsam token'dan geliyor.
- Başarısız her kabul aynı hatayı döndürüyor.
- Kabul uç noktası rate limiting kapsamında.

Frontend:

- Ekip ekranı: üye tablosu, rol değiştirme, üye çıkarma, bekleyen davetler.
- Davet diyaloğu bağlantıyı bir kez gösteriyor ve e-posta gönderiminin henüz
  devrede olmadığını açıkça söylüyor.
- Davet kabul ekranı; giriş yapılmamışsa token korunarak giriş ekranına
  yönlendiriliyor.
- `toSafeRedirect`: `next` parametresi yalnızca aynı kökenli yolları kabul
  ediyor. Açık yönlendirme, girişten hemen sonra parola sormak için ikna edici
  bir yer sunardı.

Testler: 183/183. Davet güvenliği (tek kullanım, adres bağı, süre dolumu,
iptal, hash'li saklama, ayırt edilemez hatalar) ve rol yetkilendirme matrisi
uçtan uca doğrulandı.

Bu faz sırasında çözülen üç gerçek hata:

1. `useSearchParams` statik prerender'da Suspense sınırı gerektiriyordu;
   `/giris` derlemesi kırılıyordu.
2. Render sırasında ref güncelleme (React kural ihlali) lint'e takıldı;
   TanStack Query'nin kararlı `mutate` fonksiyonu doğrudan kullanıldı.
3. Playwright doğrulamasında `networkidle` React Query ile hiç sonuçlanmıyordu;
   bekleme stratejisi açık seçici beklemeye çevrildi.

### Faz 04 — Çok Kiracılılık · Tamamlandı

Domain:

- `Tenant`, `Membership` ve `WorkspaceSlug` değer nesnesi.
- `WorkspaceSlug` Türkçe adları çevir yazıyor: "Kuzey Yazılım" →
  `kuzey-yazilim`. Noktalı/noktasız i açıkça ele alınıyor; invariant
  küçültme "I" harfini yanlış eşler, kültüre duyarlı küçültme ise sonucu
  sunucunun yereline bağlardı.
- Son sahibin sahipliği bırakması engelleniyor: sahipsiz bir çalışma alanını
  kimse yönetemezdi.
- `ITenantOwned` işaretleyicisi, global query filter'a otomatik kayıt sağlıyor.

Application:

- `ITenantContext` ve `ICurrentUser` sözleşmeleri.
- `WorkspaceAction` + `WorkspacePermissions`: izin matrisi tek bir tabloda.
  Eşlenmemiş bir eylem reddediliyor; kapalı başarısız oluyor.
- Use case'ler: Create, List, Get, Update, Delete.

API:

- `WorkspaceResolutionFilter` birincil izolasyon sınırı. Rotadaki slug'dan
  tenant'a ve üyeliğe tek sorguyla gidiyor; ikisi de yoksa `404`.
- Filtre uç nokta grubuna uygulanıyor, tek tek uç noktalara değil; böylece
  ileride eklenecek bir uç nokta onu sessizce atlayamaz.
- `TenantContext` istek başına bir kez, yalnızca çözümleme filtresi tarafından
  dolduruluyor. Aşağı akıştaki her okuyucu çözümlenmiş bir bağlamı üyelik
  kanıtı sayabiliyor.

Frontend:

- Çalışma alanı listesi, oluşturma ekranı, `/app/{slug}/dashboard` kabuğu ve
  çalışma alanı seçici.
- Adres önizlemesi render sırasında türetiliyor; effect ile state'e
  aynalanmıyor.
- Türkçe hata sınırı ve 404 sayfası eklendi.

Testler: 135/135 geçiyor. Kiracı izolasyonu için yedi zorunlu senaryo, izin
matrisi için tam kapsamlı tablo testi, `WorkspaceSlug` için Türkçe çevir yazım
testleri ve rate limiting testleri dahil.

Bu faz sırasında çözülen beş gerçek hata:

1. Bulut senkronizasyonu (iCloud Drive) 37 adet " 2." ekli yinelenmiş dosya
   üretmişti; `dotnet run` "birden fazla proje dosyası" hatası veriyordu.
   Dosyalar silindi ve desen `.gitignore`'a eklendi.
2. API enum'ları sayı olarak serileştiriyordu; arayüz rol etiketini
   bulamayıp çöküyordu. Ada göre serileştirmeye geçildi (ADR-0023).
3. `ListWorkspaces` sorgusunda `OrderBy` projeksiyondan sonra geliyordu; EF
   sorguyu çeviremiyordu. Sıralama projeksiyondan öne alındı.
4. Base UI menü etiketi grup bağlamı gerektiriyor; `DropdownMenuLabel` grup
   dışında kullanıldığı için seçici açılırken çöküyordu.
5. `not-found.tsx` bir Server Component olduğu hâlde Client Component'a
   fonksiyon prop'u geçiriyordu; üretim derlemesi kırılıyordu. Bağlantı
   `buttonVariants` ile biçimlendirildi.

### Faz 03 — Kimlik Doğrulama · Tamamlandı

Domain:

- `RefreshToken` kendi durum kurallarını koruyor: tek kullanımlık, süresi
  dolmuş veya iptal edilmiş token takas edilemez. Tek kullanım kuralı replay
  tespitinin dayanağı.
- Token'lar `FamilyId` altında gruplanıyor; bir oturum boyunca her rotasyon
  aynı aileyi sürdürüyor.

Application:

- `Result` deseni: beklenen hatalar döndürülüyor, fırlatılmıyor.
- Use case'ler: `RegisterUser`, `LoginUser`, `RefreshSession`,
  `LogoutSession`, `GetCurrentUser`.
- `SessionIssuer` token çiftini tek yerde üretiyor; kayıt, giriş ve yenileme
  aynı ömürleri paylaşıyor.
- Sözleşmeler: `IClock`, `IFlowDeskDbContext`, `IUserAccountStore`,
  `IAccessTokenIssuer`, `ISecureTokenGenerator`.

Infrastructure:

- ASP.NET Core Identity, rol tabloları olmadan (`IdentityUserContext`).
- Parola politikası uzunluk esaslı (10 karakter), karakter sınıfı zorunluluğu
  yok (NIST SP 800-63B).
- JWT üretimi ve doğrulama parametreleri aynı ayarlardan türüyor.
  `ClockSkew` sıfır.
- Refresh token SHA-256 ile hash'lenip saklanıyor.
- İlk migration: 5 tablo (Identity kullanıcı tabloları + `RefreshTokens`).

API:

- `/api/auth/register`, `/login`, `/refresh`, `/logout`, `GET /api/me`.
- Refresh çerezi: `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`.
- Üretimde `SecurePolicy` gevşetilirse uygulama başlamıyor.
- Rate limiting: login 10/dk, register 5/10dk, refresh 30/dk.
- Doğrulama hataları 422, ProblemDetails biçiminde, alan bazında.

Frontend:

- Access token modül kapsamlı bir değişkende; `localStorage` ve
  `sessionStorage` kullanılmıyor.
- Tek uçuşlu (single-flight) yenileme: aynı anda birden fazla sorgu süresi
  dolmuş token görürse tek bir yenileme yapılıyor. Aksi halde ikinci istek
  replay gibi görünür ve sunucu haklı olarak tüm aileyi iptal ederdi.
- TanStack Query ile oturum durumu; çıkışta önbellek tamamen temizleniyor.
- Giriş ve kayıt ekranları, istemci tarafı rota koruması.

Bu faz sırasında çözülen üç gerçek hata:

1. `ValidationFilter<RegisterRequest>` ile `IValidator<RegisterUserCommand>`
   eşleşmiyordu; kayıt uç noktası 500 dönüyordu. İstek gövdeleri doğrudan
   Application komutlarına bağlanarak ikizleme kaldırıldı.
2. Doğrulama hataları 400 dönüyordu; `docs/API_CONVENTIONS.md` 422 diyor. Kod
   dokümana uyduruldu.
3. `AddJwtBearer` içinde `BuildServiceProvider` çağrısı ikinci bir konteyner
   yaratıyordu; yapılandırma options pipeline'ına taşındı.

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

Faz 03 sonunda:

| Komut / kontrol | Sonuç |
|---|---|
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 51/51 başarılı |
| `dotnet ef database update` | Boş veritabanına uygulandı, 5 tablo |
| `npm --prefix frontend run lint / typecheck / format:check / build` | Başarılı |
| Tarayıcıda uçtan uca akış | Kayıt → yenileme → çıkış → hatalı giriş → giriş tamam |
| `localStorage` / `sessionStorage` | Boş (ADR-0006 doğrulandı) |
| Çerezler | Yalnızca `flowdesk_refresh_token`; `HttpOnly`, `SameSite=Strict`, `Path=/api/auth` |

Faz 07 sonunda:

| Komut / kontrol | Sonuç |
|---|---|
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 320/320 başarılı (169 birim + 151 entegrasyon) |
| `dotnet ef migrations add AddTickets` + `database update` | Uygulandı; `xmin` sistem sütunu `CREATE TABLE` çıktısında yok |
| `dotnet ef migrations add AddTicketAssigneeForeignKey` + `database update` | Uygulandı |
| `npm --prefix frontend run lint / typecheck / build` | Başarılı |
| Çalışan API'ye karşı 14 adımlık talep akışı | Tamamı geçti — numaralandırma, geçersiz geçiş `409`, eşzamanlılık `409`, izolasyon `404`, rol matrisi, silme |
| `/app/{slug}/tickets` ve `/app/{slug}/tickets/{id}` | HTTP 200, doğru başlık, uygulama hatası yok |

Faz 08 sonunda:

| Komut / kontrol | Sonuç |
|---|---|
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 377/377 başarılı (201 birim + 176 entegrasyon), altı ardışık tam koşu temiz |
| `dotnet ef migrations add AddTasks` + `database update` | Uygulandı |
| `npm --prefix frontend run lint / typecheck / build` | Başarılı |
| Çalışan API'ye karşı 14 adımlık görev akışı | Tamamı geçti — gecikme hesabı, tamamlanma tarihinin temizlenmesi, null'un alanı temizlemesi, sıralama, izolasyon `404`, rol matrisi |
| `/app/{slug}/tasks` | HTTP 200, doğru başlık, uygulama hatası yok |

Faz 09 sonunda:

| Komut / kontrol | Sonuç |
|---|---|
| `dotnet build backend/FlowDesk.slnx` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test backend/FlowDesk.slnx` | 386/386 başarılı (201 birim + 185 entegrasyon) |
| `npm --prefix frontend run lint / typecheck / build` | Başarılı |
| Çalışan API'ye karşı 11 adımlık dashboard akışı | Tamamı geçti — açık talep tanımı, atanmamış sayımı, geciken/bu hafta ayrımı, enum sıralı dağılım, izolasyon `404`, izleyici erişimi |
| `/app/{slug}/dashboard` | HTTP 200, doğru başlık, uygulama hatası yok |
| Migration | **Yok** — bu faz yalnızca okuma yapıyor |

**Doğrulama biçimi hakkında not.** Bu fazda uçtan uca akış tarayıcıda tıklanarak
değil, çalışan API'ye karşı gerçek HTTP istekleriyle doğrulandı; bu oturumda
tarayıcı otomasyonu mevcut değildi ve Playwright Faz 17'ye ait. Arayüz tarafında
lint, tip kontrolü, üretim derlemesi ve rota render'ı doğrulandı. Tıklama
seviyesindeki akış Faz 17'de Playwright ile kalıcı hâle gelecek.
