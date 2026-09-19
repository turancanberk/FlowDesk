# CLAUDE.md — FlowDesk Çalışma Kuralları

FlowDesk, küçük ve orta ölçekli ekipler için çok kiracılı (multi-tenant) bir B2B
müşteri operasyonları SaaS ürünüdür. Bu dosya kısa ve kalıcıdır; ayrıntı ilgili
dokümanlardadır.

## Yeni oturuma başlarken

Kod yazmadan önce sırayla oku ve doğrula:

1. Bu dosya
2. `docs/PROGRESS.md` — hangi faz bitti, hangisi aktif
3. `docs/HANDOFF.md` — tam olarak nerede kalındı, sıradaki somut iş
4. `docs/DECISIONS.md` — ilgili mimari kararlar (ADR)
5. `git status` ve `git log --oneline -10`

`docs/HANDOFF.md` operasyonel devir dosyasıdır ve kendi kullanım protokolünü
içerir. **Ona körlemesine güvenme**: depo gerçekliği her zaman önceliklidir.
Çelişki varsa depoyu esas al, farkı araştır, HANDOFF'u düzelt, sonra devam et.

### Source of truth hiyerarşisi

Çalışan ve test edilmiş kod → `DECISIONS.md` → `CLAUDE.md` →
`ARCHITECTURE.md` / `SECURITY.md` → `PROGRESS.md` → `HANDOFF.md` →
konuşma geçmişi. Konuşma geçmişi hiçbir zaman deponun önüne geçmez.

## Dil kuralları

| Alan | Dil |
|---|---|
| Kullanıcı arayüzü, tüm görünen metinler | Türkçe |
| Proje dokümantasyonu | Türkçe |
| Git commit açıklamaları | Türkçe (Conventional Commit ön ekleri İngilizce) |
| Kaynak kod tanımlayıcıları | İngilizce |
| Veritabanı tablo/sütun adları, API rotaları, JSON alanları | İngilizce |

`Customer`, `CreateCustomer`, `TicketStatus.InProgress`, `TenantId` doğrudur.
`Musteri`, `TalepOlustur`, `KullaniciUyelik` kullanılmaz.

Türkçe görünen metinler domain varlıklarına gömülmez; sunum katmanında
eşlenir. Örneğin `TicketStatus.InProgress` arayüzde "Devam Ediyor" olur.

Yerelleştirme `tr-TR`. Tarih ve sayı biçimlendirmesi sunum katmanında yapılır;
veritabanında biçimlendirilmiş metin değil, tipli UTC zaman damgası saklanır.

## Mimari kısıtlar

Modüler monolit. Bağımlılık yönü tek yönlüdür:
`Api`/`Worker` → `Application` → `Domain`, ve `Infrastructure` → `Application`/`Domain`.
Domain hiçbir altyapıya bağımlı değildir; bu kural mimari testlerle doğrulanır.

Kullanılmayacaklar: mikroservis, Kubernetes, generic `IRepository<T>`,
generic UnitOfWork, MediatR, event sourcing, spekülatif soyutlama.
`DbContext` zaten Unit of Work'tür.

Her soyutlamanın somut bir gerekçesi olmalıdır. SOLID kullanıldığını iddia
etmek için soyutlama eklenmez.

## Güvenlik kısıtları

- Kiracı izolasyonu bir **güvenlik sınırıdır**, kolaylık değil. Yabancı kiracı
  kaynağı için `404` döndürülür.
- Yetkilendirme backend'de zorunlu kılınır. Frontend görünürlüğü güvenlik
  sınırı sayılmaz.
- Access token yalnızca tarayıcı belleğinde tutulur, `Authorization: Bearer`
  ile gönderilir. `localStorage`/`sessionStorage` kullanılmaz.
- Refresh token `HttpOnly` + `Secure` çerezdedir, döner (rotating), veritabanında
  hash'li saklanır, replay tespiti vardır.
- Bu depo **public**'tir. Gerçek `.env`, parola, imzalama anahtarı, token, API
  anahtarı, sertifika veya kişisel veri hiçbir koşulda commit edilmez.

Ayrıntı: `docs/SECURITY.md`.

## Kademeli altyapı

Bir altyapı bileşeni, onu gerçekten kullanan faz gelmeden `docker-compose.yml`'a
eklenmez. Sıra: PostgreSQL (Faz 01) → RabbitMQ (10) → Mailpit (12) →
Azurite (13) → Redis (15) → Prometheus/Grafana (18).

## Sürüm politikası

Paket sürümleri kurulum anında resmî registry'den doğrulanır. Yalnızca stable
sürüm kullanılır; `preview`/`rc`/`beta`/`canary` kullanılmaz. Peer dependency
uyumu kontrol edilir. Backend sürümleri `Directory.Packages.props` ile merkezî
yönetilir, frontend `package-lock.json` depoda tutulur.

## Çalışma protokolü

Her fazda yalnızca o fazın kapsamı uygulanır; sonraki fazlar erken
implement edilmez.

Faz sonunda gerçekten çalıştırılır (sonuç tahmin edilmez):

```bash
dotnet build backend/FlowDesk.slnx
dotnet test  backend/FlowDesk.slnx
npm --prefix frontend run format:check
npm --prefix frontend run lint
npm --prefix frontend run typecheck
npm --prefix frontend run build
npm --prefix e2e run format:check
npm --prefix e2e run typecheck
npm --prefix e2e test        # Compose servisleri ayakta olmalı
```

Ardından: PR review disiplininde öz-değerlendirme → `PROGRESS.md` ve
`HANDOFF.md` güncelle → `git diff` ve sır kontrolü → Türkçe commit → dalı push
et → DoD sağlandıysa `main`'e `--no-ff` merge et ve push et → sonraki faz.

Test geçirmek için test silinmez, skip edilmez, assertion zayıflatılmaz,
güvenlik kontrolü kaldırılmaz, kiracı izolasyonu gevşetilmez, uyarılar
anlamsızca bastırılmaz, üretim davranışı sahte implementasyonla değiştirilmez.

## Git

`main` her zaman çalışır durumdadır. Her ana faz için `feat/*` dalı açılır.
Remote geçmiş rewrite edilmez, force push kullanılmaz. Commit geçmişi yapay
olarak şişirilmez; her commit gerçek bir mühendislik değişikliğini temsil eder.

## Dokümantasyon haritası

| Dosya | İçerik |
|---|---|
| `docs/PRODUCT.md` | Ürün tanımı, roller, kullanıcı akışları |
| `docs/ARCHITECTURE.md` | Katmanlar, bağımlılık yönü, altyapı, portlar |
| `docs/DESIGN_SYSTEM.md` | Tasarım yönü, token'lar, bileşen kuralları |
| `docs/DATABASE.md` | Veri modeli, indeksler, kısıtlar, migration |
| `docs/API_CONVENTIONS.md` | REST kuralları, hata biçimi, sayfalama |
| `docs/SECURITY.md` | Kimlik doğrulama, yetkilendirme, kiracı izolasyonu |
| `docs/DECISIONS.md` | ADR kayıtları |
| `docs/ROADMAP.md` | Faz planı ve kapsam dışı özellikler |
| `docs/PROGRESS.md` | Faz ilerlemesi, bilinen sorunlar, teknik borç |
| `docs/HANDOFF.md` | Agent devir dosyası |
