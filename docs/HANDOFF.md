# FlowDesk — Agent Devir Dosyası

Bu dosya, konuşma geçmişi olmadan projeye devam edilebilmesi için tutulur.
Kısa, güncel ve operasyonel olmalıdır.

> **Bu dosyaya körlemesine güvenme.** Depo gerçekliği her zaman önceliklidir.
> Bu dosya ile kod/Git durumu çelişiyorsa depoyu esas al, farkı araştır,
> bu dosyayı düzelt, sonra devam et.

---

## Son Güncelleme

2026-09-16 (UTC)

## Repository Durumu

| Alan | Değer |
|---|---|
| Aktif dal | `chore/bootstrap` |
| Son commit | Faz 00 commit'i sonrası güncellenecek |
| Working tree | Faz 00 commit'i ile temizlenecek |
| Remote | `origin` → GitHub `FlowDesk` (public) |
| Remote ile senkron | Faz 00 push'u sonrası |

## Tamamlanan Fazlar

- Faz 00 — Bootstrap

## Şu Anda Nerede Kaldık?

**Faz 01 — Temel** (henüz başlanmadı)

### Tamamlananlar

Faz 01 kapsamında henüz iş yapılmadı. Ön koşul olan .NET 10.0.401 SDK
kurulumu Faz 00 sırasında tamamlandı ve doğrulandı.

### Devam Eden İş

Yok.

### Henüz Yapılmayanlar

Faz 01'in tamamı:

- `backend/global.json`, `Directory.Build.props`, `Directory.Packages.props`
- `FlowDesk.sln` ve yedi proje (Domain, Application, Infrastructure, Api,
  Worker, UnitTests, IntegrationTests)
- Nullable reference types, analyzer'lar, uyarı politikası
- Next.js uygulaması, TypeScript strict, Tailwind, ESLint
- `infra/docker-compose.yml` — yalnızca PostgreSQL
- `/health/live` ve `/health/ready` uç noktaları
- Bağımlılık yönünü doğrulayan mimari test

## Bir Sonraki Yapılacak İş

`feat/foundation` dalını aç. `backend/global.json` dosyasını .NET 10.0.401'e
sabitleyerek oluştur, ardından `dotnet new sln` ile `FlowDesk.sln` ve yedi
projeyi oluştur. Proje referanslarını ADR-0001'deki bağımlılık yönüne göre
bağla: `Api`/`Worker` → `Application` → `Domain`, `Infrastructure` →
`Application`/`Domain`. `dotnet build` ile doğrula.

Paket sürümlerini eklerken resmî registry'den doğrula (ADR-0016); plandaki veya
dokümandaki sürüm numaralarını körlemesine kullanma.

## Son Doğrulama Durumu

Gerçekten çalıştırılan komutlar:

| Komut | Sonuç |
|---|---|
| `dotnet --version` | `10.0.401` |
| `dotnet --list-sdks` | 8.0.421, 9.0.314, 10.0.401 |
| `dotnet --list-runtimes` | `Microsoft.NETCore.App 10.0.12`, `Microsoft.AspNetCore.App 10.0.12` |
| `dotnet ef --version` | `10.0.8` |
| `gh auth status` | `turancanberk` olarak giriş yapılmış, `repo` + `workflow` kapsamları var |

Backend, frontend ve Docker doğrulamaları Faz 01'de ilk kez çalıştırılacak.

## Mevcut Hatalar / Blokerler

Bilinen blocker yok.

Çözülmüş ortam sorunu (tekrarlarsa): macOS 26 / Apple Silicon üzerinde
`dotnet-install.sh` mevcut bir `~/.dotnet` kurulumunun üzerine yazdığında host
ikilisi `SIGKILL (Code Signature Invalid)` alır. Crash raporunda sonlanma
nedeni `CODESIGNING / Taskgated Invalid Signature` görünür. `codesign -v`
imzayı geçerli bulur; sorun çekirdeğin o inode için tuttuğu bayat
değerlendirmedir. Çözüm:

```bash
cd ~/.dotnet && cp dotnet dotnet.new && mv -f dotnet.new dotnet && chmod +x dotnet
```

## Önemli Mimari Kararlar

Ayrıntı `docs/DECISIONS.md` içindedir; burada yalnızca hatırlatma:

- Modüler monolit, mikroservis yok (ADR-0001)
- Paylaşılan veritabanı + paylaşılan şema çok kiracılılık (ADR-0002)
- User / Tenant / Membership; rol `Membership` üzerinde (ADR-0003)
- MediatR yok, generic repository yok, generic UnitOfWork yok (ADR-0004)
- Yol tabanlı workspace rotalama (ADR-0005)
- Access token yalnızca bellekte; refresh token `HttpOnly` çerezde, döner,
  hash'li saklanır, replay tespiti var (ADR-0006)
- Yabancı kiracı kaynağı için `404` (ADR-0007)
- Kademeli altyapı: bileşen, onu kullanan faz gelmeden Compose'a eklenmez
  (ADR-0008)
- Kimlikli veri çekme istemci tarafında; Server Component'lar access token'a
  erişemez (ADR-0009)
- .NET 10 LTS (ADR-0010), TypeScript 6 hattı (ADR-0011)
- Soft delete yalnızca `Customer` (ADR-0012)
- İyimser eşzamanlılık yalnızca `Ticket` (ADR-0013)
- Outbox + at-least-once idempotency (ADR-0014)
- Redis yalnızca dashboard önbelleği (ADR-0015)
- Merkezî paket yönetimi, kurulum anında sürüm doğrulama (ADR-0016)

Kiracı izolasyonu bir **güvenlik sınırıdır**. Kullanıcı arayüzü Türkçe, kaynak
kod tanımlayıcıları İngilizce, commit mesajları Türkçe.

## Değiştirilen Önemli Dosyalar

Faz 00'da oluşturulanlar: `.gitignore`, `.editorconfig`, `.env.example`,
`CLAUDE.md`, `README.md` ve `docs/` altındaki tüm dokümanlar.

## Database Durumu

| Alan | Durum |
|---|---|
| Son migration | Yok |
| Migration uygulandı mı | Hayır — henüz DbContext yok |
| Seed | Yok (Faz 21) |

## Infrastructure Durumu

Kademeli altyapı planı gereği (ADR-0008) henüz hiçbir servis Compose'a
eklenmedi.

| Servis | Durum |
|---|---|
| PostgreSQL | Faz 01'de eklenecek |
| RabbitMQ | Henüz projeye eklenmedi (Faz 10) |
| Mailpit | Henüz projeye eklenmedi (Faz 12) |
| Azurite | Henüz projeye eklenmedi (Faz 13) |
| Redis | Henüz projeye eklenmedi (Faz 15) |
| Prometheus / Grafana | Henüz projeye eklenmedi (Faz 18) |

Not: Bu makinede host portları `5432`, `6379` ve `5000` başka süreçlerce
kullanılıyor. Port haritası `docs/ARCHITECTURE.md` içindedir; PostgreSQL 5433,
Redis 6380, API 5080 kullanır.

## Harici İşlemler

Kullanıcıdan beklenen harici işlem yok.

---

## Sonraki Agent İçin Başlangıç Talimatı

1. `CLAUDE.md` oku.
2. `docs/PROGRESS.md` oku — hangi faz bitti, hangisi aktif.
3. Bu dosyayı oku.
4. `docs/DECISIONS.md` içinde ilgili ADR'leri oku.
5. `git status` çalıştır.
6. `git log --oneline -10` ile son commit'leri incele.
7. Bu dosyadaki "Son Doğrulama Durumu" bölümündeki komutları gerektiğinde
   yeniden çalıştır ve sonuçların hâlâ geçerli olduğunu doğrula.
8. "Bir Sonraki Yapılacak İş" bölümünden devam et.
9. Önceki tamamlanmış işi sebepsiz yere yeniden yazma.

### Bu dosyanın güncellenme zamanları

Her faz tamamlandığında; önemli bir alt özellik bittiğinde; yeni migration
sonrası; önemli bir mimari karar sonrası; bir blocker ortaya çıktığında; uzun
bir implementasyona başlamadan önce durum değiştiyse; oturumun güvenli şekilde
kesilebileceği anlamlı checkpoint'lerde.

Gereksiz sık güncelleme commit'i oluşturulmaz.

### Faz ortasında durmak gerekirse

1. Kodu derlenebilir güvenli bir noktaya getir.
2. İlgili testleri çalıştır.
3. Yarım implementasyonu gizleme.
4. Hangi kısmın tamamlanmadığını bu dosyaya açıkça yaz.
5. `docs/PROGRESS.md` içinde fazı tamamlanmış olarak **işaretleme**.
6. Gerçek bir checkpoint commit'i oluştur (bozuk kod commit etme).
7. Remote'a push et.
8. "Bir Sonraki Yapılacak İş" bölümünü somut biçimde güncelle.
