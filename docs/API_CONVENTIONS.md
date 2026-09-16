# FlowDesk — API Kuralları

REST, kaynak odaklı rotalar, tahmin edilebilir davranış.

Rota ve JSON alan adları İngilizce'dir. Kullanıcıya gösterilen hata metinleri
Türkçe'dir ve frontend tarafından sunulur.

## Rota yapısı

### Kimlik doğrulama

```
POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/auth/logout
GET    /api/me
```

### Çalışma alanları

```
GET    /api/workspaces                      # kullanıcının üye olduğu workspace'ler
POST   /api/workspaces                      # yeni workspace (oluşturan Owner olur)
GET    /api/workspaces/{workspaceSlug}
PATCH  /api/workspaces/{workspaceSlug}
DELETE /api/workspaces/{workspaceSlug}
```

### Tenant kapsamlı kaynaklar

```
GET    /api/workspaces/{workspaceSlug}/members
PATCH  /api/workspaces/{workspaceSlug}/members/{userId}
DELETE /api/workspaces/{workspaceSlug}/members/{userId}

GET    /api/workspaces/{workspaceSlug}/invitations
POST   /api/workspaces/{workspaceSlug}/invitations
DELETE /api/workspaces/{workspaceSlug}/invitations/{id}
POST   /api/invitations/accept                       # tenant dışı: token ile

GET    /api/workspaces/{workspaceSlug}/customers
POST   /api/workspaces/{workspaceSlug}/customers
GET    /api/workspaces/{workspaceSlug}/customers/{id}
PATCH  /api/workspaces/{workspaceSlug}/customers/{id}
DELETE /api/workspaces/{workspaceSlug}/customers/{id}   # arşivleme

GET    /api/workspaces/{workspaceSlug}/tickets
POST   /api/workspaces/{workspaceSlug}/tickets
GET    /api/workspaces/{workspaceSlug}/tickets/{id}
PATCH  /api/workspaces/{workspaceSlug}/tickets/{id}
DELETE /api/workspaces/{workspaceSlug}/tickets/{id}
POST   /api/workspaces/{workspaceSlug}/tickets/{id}/comments
GET    /api/workspaces/{workspaceSlug}/tickets/{id}/comments
POST   /api/workspaces/{workspaceSlug}/tickets/{id}/attachments
GET    /api/workspaces/{workspaceSlug}/tickets/{id}/attachments/{attachmentId}

GET    /api/workspaces/{workspaceSlug}/tasks
POST   /api/workspaces/{workspaceSlug}/tasks
GET    /api/workspaces/{workspaceSlug}/tasks/{id}
PATCH  /api/workspaces/{workspaceSlug}/tasks/{id}
DELETE /api/workspaces/{workspaceSlug}/tasks/{id}

GET    /api/workspaces/{workspaceSlug}/activity
GET    /api/workspaces/{workspaceSlug}/dashboard
GET    /api/workspaces/{workspaceSlug}/notifications
```

### Sağlık

```
GET    /health/live      # süreç ayakta mı
GET    /health/ready     # zorunlu bağımlılıklar hazır mı
```

### Bilinçli sapmalar

- **`POST /api/invitations/accept`** workspace kapsamı dışındadır. Daveti kabul
  eden kullanıcı henüz o workspace'in üyesi değildir; tenant kapsamlı rotanın
  üyelik doğrulaması bu isteği reddederdi. Kapsam token'ın kendisinden çözülür.
- **`DELETE /api/workspaces/{slug}/customers/{id}`** kalıcı silmez, arşivler
  (ADR-0012). Fiil `DELETE` olarak korunur çünkü istemci açısından anlam
  "bu kaydı listeden kaldır"dır.

## HTTP fiilleri

| Fiil | Anlam |
|---|---|
| `GET` | Okuma, yan etkisiz |
| `POST` | Oluşturma veya eylem |
| `PATCH` | Kısmi güncelleme |
| `DELETE` | Silme veya arşivleme |

`PUT` kullanılmaz; kısmi güncelleme ürünün gerçek ihtiyacıdır ve `PATCH` bunu
doğru ifade eder.

## Durum kodları

| Kod | Kullanım |
|---|---|
| `200 OK` | Başarılı okuma veya güncelleme |
| `201 Created` | Oluşturma; `Location` başlığı döner |
| `204 No Content` | Gövdesiz başarılı işlem (silme, logout) |
| `400 Bad Request` | Bozuk istek |
| `401 Unauthorized` | Kimlik doğrulanamadı veya token geçersiz |
| `403 Forbidden` | Üye ama rolü yetersiz |
| `404 Not Found` | Kayıt yok **veya yabancı kiracıya ait** (ADR-0007) |
| `409 Conflict` | Eşzamanlılık çakışması veya benzersizlik ihlali |
| `422 Unprocessable Entity` | Doğrulama hatası |
| `429 Too Many Requests` | Rate limit aşıldı |
| `500 Internal Server Error` | Beklenmeyen hata |

`403` ile `404` ayrımı önemlidir: kullanıcı tenant'ın **üyesiyse** ama rolü
yetersizse `403` döner. Üye **değilse** `404` döner; böylece workspace'in veya
kaydın varlığı sızmaz.

## Hata biçimi

Tüm hatalar RFC 9457 `ProblemDetails` biçiminde döner.

```json
{
  "type": "https://flowdesk.app/problems/validation",
  "title": "Doğrulama hatası",
  "status": 422,
  "detail": "Gönderilen alanlardan bazıları geçersiz.",
  "instance": "/api/workspaces/acme/customers",
  "traceId": "00-4bf92f...-01",
  "errors": {
    "email": ["Geçerli bir e-posta adresi girin."],
    "name": ["Ad alanı zorunludur."]
  }
}
```

Kurallar:

- Üretim yanıtlarında **stack trace bulunmaz**.
- İç istisna ayrıntıları yanıta konmaz; yapılandırılmış log'a yazılır.
- `traceId` yanıtta döner; bir hata raporunu log'daki kayıtla eşleştirmeyi
  sağlar.
- Doğrulama hataları `errors` sözlüğünde alan bazında toplanır.

## Sayfalama

Liste uç noktaları sınırsız koleksiyon döndürmez.

| Parametre | Varsayılan | Sınır |
|---|---|---|
| `page` | 1 | ≥ 1 |
| `pageSize` | 25 | maksimum 100 |
| `sort` | kaynağa özgü | beyaz liste |
| `search` | — | |

Yanıt:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalCount": 137,
  "totalPages": 6
}
```

`pageSize` üst sınırı aşarsa hata dönmez, sınıra kırpılır; istemci hatası
yüzünden kullanıcı akışı bozulmaz ancak sunucu korunur.

### Kararlı sıralama

Sıralama alanı benzersiz değilse (örneğin `name`) ikincil olarak `Id` eklenir.
Aksi halde sayfalar arasında kayıt tekrarlanabilir veya atlanabilir.

Sıralama alanları beyaz listeden doğrulanır; istemciden gelen değer doğrudan
sorguya konmaz.

## Filtreleme

Filtreler sorgu parametresi olarak geçer ve enum değerleri **İngilizce kod**
adıyla gönderilir:

```
GET /api/workspaces/acme/tickets?status=Open&priority=Urgent&assignedUserId=...
GET /api/workspaces/acme/customers?status=Active&search=nova
GET /api/workspaces/acme/tasks?status=Todo&dueBefore=2026-10-01T00:00:00Z
```

Türkçe etiketler yalnızca sunum katmanındadır; API sözleşmesine girmez.

## İstek ve yanıt modelleri

- EF Core varlıkları **doğrudan dışa verilmez**. Açık request/response DTO'ları
  kullanılır.
- Request modelleri yalnızca istemcinin kontrol edebileceği alanları içerir.
  `TenantId`, `CreatedByUserId`, `CreatedAt`, `Number` gibi alanlar istemciden
  alınmaz; sunucuda belirlenir.
- Response modelleri yalnızca bilinçli olarak açılan alanları içerir.

Bu ayrım overposting'i önler, iç şemayı gizler ve veritabanı ile API
sözleşmesini birbirinden ayırır.

## Tarih ve saat

Tüm zaman damgaları **UTC** ve ISO 8601 biçiminde taşınır
(`2026-09-16T12:34:56Z`). Yerelleştirme ve `tr-TR` biçimlendirmesi yalnızca
frontend'de yapılır.

## Eşzamanlılık

`Ticket` güncellemelerinde iyimser eşzamanlılık uygulanır (ADR-0013). İstemci
okuduğu sürüm belirtecini geri gönderir; kayıt bu arada değiştiyse `409 Conflict`
döner ve istemci kullanıcıya Türkçe bir uyarı göstererek yeniden yüklemeyi
önerir.

## Kimlik doğrulama

Korumalı uç noktalar `Authorization: Bearer <accessToken>` başlığı bekler.
Refresh token yalnızca `POST /api/auth/refresh` ve `POST /api/auth/logout`
uç noktalarında, `HttpOnly` çerez üzerinden taşınır (ADR-0006).
