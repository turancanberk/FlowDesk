# FlowDesk — Ürün Tanımı

## Özet

FlowDesk, küçük ve orta ölçekli ekiplerin müşteri operasyonlarını tek yerden
yürütmesini sağlayan çok kiracılı bir B2B SaaS ürünüdür. Her organizasyon kendi
çalışma alanına (workspace) sahiptir; müşterilerini, destek taleplerini,
görevlerini ve ekibini bu alan içinde yönetir.

Ürün dili Türkçe'dir.

## Çözdüğü problem

Küçük ekipler müşteri bilgisini e-tabloda, destek taleplerini e-posta kutusunda,
görevleri ayrı bir araçta tutar. Bilgi dağılır, sorumluluk belirsizleşir, geçmiş
kaybolur. FlowDesk bu üç akışı tek bir operasyonel yüzeyde birleştirir ve her
önemli eylemi izlenebilir kılar.

## Temel kavramlar

| Kavram | Açıklama |
|---|---|
| **User** | Sisteme kayıtlı kişi. Birden fazla organizasyona üye olabilir. |
| **Tenant** (workspace) | Bir organizasyonun izole veri alanı. |
| **Membership** | Bir kullanıcının belirli bir tenant içindeki üyeliği ve rolü. |
| **Customer** | Workspace'in hizmet verdiği müşteri kaydı. |
| **Ticket** | Bir müşteriye ait destek talebi. |
| **TicketComment** | Talep üzerindeki yorum. |
| **Task** | Ekip içi görev; isteğe bağlı olarak bir müşteriye bağlanabilir. |
| **Invitation** | Bir kişiyi workspace'e davet eden, süreli ve tek kullanımlık kayıt. |
| **ActivityEvent** | Önemli eylemlerin denetlenebilir kaydı. |
| **Attachment** | Talebe eklenen dosya. |

Bir kullanıcı aynı anda birden fazla workspace'e üye olabilir ve aralarında
geçiş yapar. Örneğin Acme Teknoloji'de Owner, Kuzey Yazılım'da Agent olabilir.
**Acme verisi Kuzey üzerinden hiçbir koşulda görünmez.** Kiracı izolasyonu bir
güvenlik sınırıdır.

## Roller

| Rol | Türkçe karşılık | Kapsam |
|---|---|---|
| `Owner` | Sahip | Workspace üzerinde tam yetki, sahiplik devri, workspace silme |
| `Admin` | Yönetici | Ekip ve davet yönetimi, tüm operasyonel veriler üzerinde tam yetki |
| `Agent` | Temsilci | Müşteri, talep ve görev oluşturma/güncelleme |
| `Viewer` | İzleyici | Yalnızca okuma |

Ayrıntılı izin matrisi `docs/SECURITY.md` içindedir ve backend'de merkezî olarak
zorunlu kılınır.

## Hedef kullanıcı akışı

1. Kullanıcı kayıt olur.
2. Giriş yapar.
3. Bir workspace oluşturur ve otomatik olarak Sahip (Owner) olur.
4. Ekip arkadaşlarını e-posta ile davet eder.
5. Davet edilen kişiler daveti kabul ederek workspace'e katılır.
6. Ekip müşteri kayıtları oluşturur.
7. Müşteriler için destek talepleri açılır.
8. Talepler ekip üyelerine atanır.
9. Görevler oluşturulur ve son tarih verilir.
10. Görevler isteğe bağlı olarak bir müşteriye bağlanır.
11. Talep üzerinde yorumlar yazılır.
12. İlgili kullanıcılar bildirim alır.
13. Önemli eylemler etkinlik geçmişinde görünür.
14. Dashboard workspace'in güncel durumunu özetler.

## Durum modelleri

Domain tanımlayıcıları İngilizce, kullanıcıya görünen etiketler Türkçe'dir.

### Talep durumu (`TicketStatus`)

| Kod | Görünen |
|---|---|
| `Open` | Açık |
| `InProgress` | Devam Ediyor |
| `Waiting` | Bekliyor |
| `Resolved` | Çözüldü |
| `Closed` | Kapalı |

### Talep önceliği (`TicketPriority`)

| Kod | Görünen |
|---|---|
| `Low` | Düşük |
| `Medium` | Orta |
| `High` | Yüksek |
| `Urgent` | Acil |

### Görev durumu (`TaskItemStatus`)

Sınıf adları `TaskItem` ve `TaskItemStatus`'tır; `Task` ve `TaskStatus`
`System.Threading.Tasks` ile çakışır (ADR-0029). API'de değerler yine
`Todo` / `InProgress` / `Done` olarak taşınır.

| Kod | Görünen |
|---|---|
| `Todo` | Yapılacak |
| `InProgress` | Devam Ediyor |
| `Done` | Tamamlandı |

### Müşteri durumu (`CustomerStatus`)

| Kod | Görünen |
|---|---|
| `Active` | Aktif |
| `Inactive` | Pasif |

## Gezinme yapısı

Çalışma alanı rotaları yol tabanlıdır: `/app/{workspaceSlug}/...`

```
FLOWDESK
[Çalışma alanı seçici]

GENEL BAKIŞ
  Dashboard

MÜŞTERİ OPERASYONU
  Müşteriler
  Talepler
  Görevler

YÖNETİM
  Ekip
  Etkinlik

Ayarlar
[Kullanıcı kimliği]
```

## Dashboard içeriği

Yalnızca operasyonel karar verdiren bilgiler gösterilir: müşteri sayısı, açık
talep sayısı, yaklaşan ve geciken görevler, ekip üyesi sayısı, talep durum
dağılımı ve son etkinlikler. Ekranı doldurmak için anlamsız gösterge
üretilmez.

## Kapsam dışı

Koyu tema, Google OAuth, wildcard subdomain, Kanban görünümü, WebSocket ve
gerçek zamanlı varlık, faturalandırma, mobil uygulama ve özelleştirilebilir iş
akışları çekirdek kapsamın dışındadır. Gerekçeler ve olası gelecek planı
`docs/ROADMAP.md` içindedir.
