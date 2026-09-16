# FlowDesk — İlerleme

Bu dosya faz seviyesindeki ilerlemeyi takip eder. Her faz sonunda güncellenir.
Operasyonel devir ayrıntısı için `docs/HANDOFF.md`.

**Son güncelleme:** 2026-09-16

---

## Fazlar

- [x] Faz 00 — Bootstrap
- [ ] Faz 01 — Temel
- [ ] Faz 02 — Tasarım Sistemi
- [ ] Faz 03 — Kimlik Doğrulama
- [ ] Faz 04 — Çok Kiracılılık
- [ ] Faz 05 — Ekip ve Davetler
- [ ] Faz 06 — Müşteriler
- [ ] Faz 07 — Talepler
- [ ] Faz 08 — Görevler
- [ ] Faz 09 — Dashboard
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

**Faz 01 — Temel**

Durum: Başlanmadı

---

## Faz geçmişi

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

| Kontrol | Durum |
|---|---|
| `dotnet --version` | 10.0.401 |
| `dotnet ef --version` | 10.0.8 |
| Backend derleme | Henüz proje yok (Faz 01) |
| Backend testler | Henüz test yok (Faz 01) |
| Frontend lint / typecheck / build | Henüz proje yok (Faz 01) |
| Docker Compose | Henüz dosya yok (Faz 01) |
