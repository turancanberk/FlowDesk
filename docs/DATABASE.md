# FlowDesk — Veritabanı

PostgreSQL 17, Entity Framework Core 10, code-first migration.

Tablo ve sütun adları İngilizce'dir. Zaman damgaları **UTC** olarak, tipli
(`timestamptz`) saklanır; biçimlendirme sunum katmanında yapılır. Yerelleştirilmiş
tarih veya sayı metni veritabanına yazılmaz.

> Bu doküman hedef veri modelini tanımlar. Tablolar ilgili faz geldiğinde
> migration ile oluşturulur; güncel durum `docs/PROGRESS.md` içindedir.

## Genel kurallar

- Birincil anahtarlar `uuid` (v7 tercih edilir — zaman sıralı olduğu için
  indeks parçalanmasını azaltır).
- Kiracıya ait her iş varlığı `TenantId` sütunu taşır.
- Silme davranışı bilinçli seçilir; kazara zincirleme silme (cascade)
  oluşturulmaz.
- İndeksler körlemesine değil, gerçek sorgu desenine göre eklenir.

## Varlıklar

### Identity (Faz 03)

ASP.NET Core Identity tabloları kullanılır (`AspNetUsers`, `AspNetRoles` vb.).
`AspNetRoles` **uygulama rolleri için kullanılmaz**; roller `Membership`
üzerindedir (ADR-0003).

#### RefreshToken

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `UserId` | uuid | FK → `AspNetUsers`, cascade delete |
| `FamilyId` | uuid | Oturum ailesi; replay tespiti için |
| `TokenHash` | bytea/text | **Ham token saklanmaz**, SHA-256 hash'i |
| `ExpiresAt` | timestamptz | |
| `CreatedAt` | timestamptz | |
| `UsedAt` | timestamptz? | Dolu ise token dönmüş demektir |
| `RevokedAt` | timestamptz? | |

İndeksler: `TokenHash` (unique), `(UserId, FamilyId)`, `ExpiresAt` (temizlik
işi için).

### Tenant (Faz 04)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `Name` | text | Görünen ad |
| `Slug` | text | URL bileşeni |
| `CreatedAt` | timestamptz | |

Kısıt: `Slug` **unique**.

Satır, ekip değişikliklerinin (üye çıkarma, rol değiştirme) kilit noktasıdır:
bu işlemler transaction içinde `SELECT … FOR NO KEY UPDATE` ile satırı kilitler,
sonra sahipleri sayar. `FOR UPDATE` değil, çünkü çalışma alanına referans veren
her ekleme yabancı anahtar denetimi için bu satırda `FOR KEY SHARE` alır ve
`FOR UPDATE` onları bekletirdi (ADR-0038).

### Membership (Faz 04)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `UserId` | uuid | FK → `AspNetUsers` |
| `TenantId` | uuid | FK → `Tenants` |
| `Role` | int | `Owner` / `Admin` / `Agent` / `Viewer` |
| `JoinedAt` | timestamptz | |

Kısıt: `(UserId, TenantId)` **unique** — bir kullanıcı aynı tenant'a iki kez
üye olamaz.
İndeks: `(TenantId, Role)` — üye listeleme ve Owner sayısı kontrolü için.

### Invitation (Faz 05)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | FK → `Tenants` |
| `Email` | citext/text | Normalize edilmiş |
| `Role` | int | Davet edilen rol |
| `TokenHash` | bytea/text | **Ham token saklanmaz** |
| `ExpiresAt` | timestamptz | |
| `AcceptedAt` | timestamptz? | Dolu ise tekrar kabul edilemez |
| `CreatedAt` | timestamptz | |
| `CreatedByUserId` | uuid | |

İndeksler: `TokenHash` (unique) — kabul akışının tek sorgusu;
`(TenantId, Email)` — aynı kişiye mükerrer bekleyen davet kontrolü.

### Customer (Faz 06)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | FK → `Tenants` |
| `Name` | text | |
| `Email` | text? | |
| `Phone` | text? | |
| `Company` | text? | |
| `Status` | int | `Active` / `Inactive` |
| `Notes` | text? | |
| `CreatedAt` | timestamptz | |
| `UpdatedAt` | timestamptz | |
| `ArchivedAt` | timestamptz? | Soft delete (ADR-0012) |

İndeksler: `(TenantId, ArchivedAt)` — varsayılan listeleme;
`(TenantId, Name)` — sıralama ve arama;
ad/şirket üzerinde arama için trigram indeks (gerekliliği ölçülerek eklenir).

### Ticket (Faz 07)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | FK → `Tenants` |
| `Number` | int | Kiracı içinde sıralı, kullanıcıya `TLP-1042` olarak gösterilir |
| `CustomerId` | uuid | FK → `Customers`, restrict |
| `Subject` | text | |
| `Description` | text | |
| `Status` | int | `Open`/`InProgress`/`Waiting`/`Resolved`/`Closed` |
| `Priority` | int | `Low`/`Medium`/`High`/`Urgent` |
| `AssignedUserId` | uuid? | FK → `AspNetUsers`, set null |
| `CreatedByUserId` | uuid | |
| `CreatedAt` | timestamptz | |
| `UpdatedAt` | timestamptz | |
| `ResolvedAt` | timestamptz? | |
| `xmin` | (sistem) | İyimser eşzamanlılık belirteci (ADR-0013) |

Kısıt: `(TenantId, Number)` **unique**.
İndeksler: `(TenantId, Status)`, `(TenantId, AssignedUserId)`,
`(TenantId, CustomerId)`.

Silme davranışı: müşteri silindiğinde talepler **silinmez** (restrict); zaten
müşteriler arşivlenir, kalıcı silinmez.

#### Talep numarası üretimi

Kiracı başına sıralı numara, `TenantCounters` tablosundaki satırın aynı
transaction içinde kilitlenip artırılmasıyla üretilir. Böylece eşzamanlı talep
oluşturma işlemlerinde numara çakışması oluşmaz.

#### Tek seferlik geçişler

Tek bir kez olması gereken durum geçişleri okuma-sonra-yazma ile değil,
koşullu güncellemeyle yapılır (ADR-0038):

| Geçiş | Koşul |
|---|---|
| Refresh token'ın harcanması | `UsedAt IS NULL AND RevokedAt IS NULL` |
| Davetin kabulü | `AcceptedAt IS NULL AND RevokedAt IS NULL` |

Etkilenen satır sayısı 0 ise istek yarışı kaybetmiştir. Halef token ya da
üyelik aynı transaction'da yazılır.

### TicketComment (Faz 07)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | |
| `TicketId` | uuid | FK → `Tickets`, cascade |
| `AuthorUserId` | uuid | |
| `Body` | text | |
| `CreatedAt` | timestamptz | |

İndeks: `(TicketId, CreatedAt)`.

### TaskItem (Faz 08)

Sınıf adı `TaskItem`, durum enum'u `TaskItemStatus`'tır; `Task` ve `TaskStatus`
adları `System.Threading.Tasks` ile çakışır (ADR-0029). Kullanıcıya "Görev"
olarak gösterilir.

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | |
| `Title` | text | |
| `Description` | text? | |
| `Status` | int | `Todo`/`InProgress`/`Done` |
| `DueAt` | timestamptz? | İsteğe bağlı; zorunlu tarih uydurulmuş son tarih üretir |
| `AssignedUserId` | uuid? | set null |
| `CustomerId` | uuid? | İsteğe bağlı ilişki, set null |
| `CreatedByUserId` | uuid | |
| `CreatedAt` | timestamptz | |
| `UpdatedAt` | timestamptz | |
| `CompletedAt` | timestamptz? | Tamamlandığında yazılır, geri alındığında **temizlenir** (ADR-0030) |

İndeksler: `(TenantId, Status)`, `(TenantId, DueAt)` — yaklaşan ve geciken
görevler, `(TenantId, AssignedUserId)`.

### OutboxMessage (Faz 11)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK, mesaj kimliği olarak da kullanılır |
| `Type` | text | Olay tipi |
| `RoutingKey` | text | Yayınlama anahtarı — tipten türetilmez (ADR-0033) |
| `Payload` | jsonb | Serileştirilmiş olay |
| `OccurredAt` | timestamptz | |
| `ProcessedAt` | timestamptz? | |
| `AttemptCount` | int | |
| `LastError` | text? | Hata görünürlüğü |
| `NextAttemptAt` | timestamptz? | Geri çekilme (backoff) |

İndeks: bekleyen kayıtlar için kısmi indeks —
`(NextAttemptAt) WHERE ProcessedAt IS NULL`. İşlenmiş kayıtlar indekste yer
tutmaz.

### ProcessedMessage (Faz 11)

| Sütun | Tip | Not |
|---|---|---|
| `MessageId` | uuid | Bileşik PK'nın parçası |
| `Consumer` | text | Aynı mesajı birden fazla tüketici işleyebilir |
| `ProcessedAt` | timestamptz | |

Birincil anahtar `(MessageId, Consumer)` **bileşiktir**. Yalnızca `MessageId`
anahtar olsaydı bir mesajı ancak tek bir tüketici kaydedebilirdi ve tablonun
varlık sebebi olan "aynı mesajı birden fazla tüketici işleyebilir" cümlesi
yanlış olurdu (ADR-0033).

Bileşik anahtar aynı zamanda benzersizlik kısıtıdır: tekrar teslimat insert
sırasında takılır ve yan etki ikinci kez uygulanmaz.

### Notification (Faz 12)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | |
| `UserId` | uuid | Alıcı |
| `Type` | int | |
| `Payload` | jsonb | Görüntüleme için bağlam |
| `ReadAt` | timestamptz? | |
| `CreatedAt` | timestamptz | |

İndeks: `(UserId, ReadAt, CreatedAt)` — okunmamış bildirim listesi.

### Attachment (Faz 13)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | |
| `TicketId` | uuid | FK → `Tickets`, cascade |
| `FileName` | text | Temizlenmiş görünen ad |
| `ContentType` | text | Beyaz listeden doğrulanmış |
| `SizeInBytes` | bigint | |
| `StorageKey` | text | **Sunucuda üretilir**, tenant kimliğini içerir |
| `UploadedByUserId` | uuid | |
| `CreatedAt` | timestamptz | |

İndeks: `(TicketId)`.

### ActivityEvent (Faz 14)

| Sütun | Tip | Not |
|---|---|---|
| `Id` | uuid | PK |
| `TenantId` | uuid | |
| `ActorUserId` | uuid? | Sistem olayları için null |
| `Type` | int | `CustomerCreated`, `TicketAssigned` vb. |
| `SubjectType` | int | İlgili varlık türü |
| `SubjectId` | uuid | |
| `Payload` | jsonb | Görüntüleme bağlamı, hassas veri içermez |
| `OccurredAt` | timestamptz | |

İndeksler: `(TenantId, OccurredAt DESC)` — etkinlik akışı;
`(TenantId, SubjectType, SubjectId)` — kayda özel geçmiş.

### TenantCounters (Faz 07)

| Sütun | Tip | Not |
|---|---|---|
| `TenantId` | uuid | PK |
| `NextTicketNumber` | int | Kilitlenerek artırılır |

---

## Global query filter

Kiracıya ait varlıklar geçerli `TenantId` üzerinden, `Customer` ayrıca
`ArchivedAt IS NULL` koşuluyla süzülür.

Bu bir **güvenlik ağıdır, tek başına güvenlik sınırı değildir**:
`IgnoreQueryFilters` ile atlanabilir ve ham SQL'i kapsamaz. Birincil sınır
üyelik doğrulamasıdır (`docs/SECURITY.md`).

## Migration

Migration'lar `FlowDesk.Infrastructure` içinde tutulur, başlangıç projesi
`FlowDesk.Api`'dir.

```bash
# Yeni migration
dotnet ef migrations add <Ad> \
  -p backend/src/FlowDesk.Infrastructure \
  -s backend/src/FlowDesk.Api

# Uygula
dotnet ef database update \
  -p backend/src/FlowDesk.Infrastructure \
  -s backend/src/FlowDesk.Api

# Üretilecek SQL'i incele (uygulamadan önce)
dotnet ef migrations script \
  -p backend/src/FlowDesk.Infrastructure \
  -s backend/src/FlowDesk.Api
```

Uygulama boş bir PostgreSQL veritabanından bu komutlarla kurulabilmelidir. Bu
Faz 23'te açıkça doğrulanır.

## Demo verisi (Faz 21)

Şemadan ayrı bir adımdır ve migration'ın parçası değildir: her üretim
başlangıcının sahte müşteri yazması istenmez.

```bash
dotnet run --project backend/src/FlowDesk.Api -- --seed-demo-data
```

Seed, ham SQL yerine domain varlıkları üzerinden yazar. Talep numarası
`TenantCounters` satırından alınır, durum geçişleri varlığın izin verdiği
yollardan geçer. Bunun bir sırası var: çalışma alanı ve müşterileri talepler
yazılmadan önce kaydedilir, çünkü sayaç satırı ham SQL ile kilitlenir ve ham
SQL değişiklik izleyicisinde bekleyeni göremez. Tamamı tek transaction'dır.

Seed hiçbir outbox satırı yazmaz; anlattığı işler geçmişte kalmıştır.
Bildirimler doğrudan yazılır. Ayrıntı ve gerekçe: ADR-0045.

## Sorgu kalitesi

ADR-0004 gereği EF Core doğrudan kullanılır; bu, sorgu kalitesini doğrudan
görünür kılar. Kaçınılacaklar: N+1 sorgular, tüm tabloyu belleğe alma, erken
`ToList`, salt okunur senaryolarda gereksiz tracking, değerlendirilmemiş büyük
`Include` grafikleri.

Kullanılacaklar: projeksiyon (`Select`), salt okunur sorgularda `AsNoTracking`,
sayfalama, `CancellationToken`, ve önemli yollarda üretilen SQL'in incelenmesi.
