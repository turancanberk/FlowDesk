using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Activity;
using FlowDesk.Domain.Activity;
using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Notifications;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.DemoData;

/// <summary>
/// Fills an empty database with a support desk that looks like it has been in
/// use for a while.
/// </summary>
/// <remarks>
/// Writes through the domain entities rather than raw SQL, so the demo obeys
/// the same rules as real use: ticket numbers come from the workspace counter,
/// statuses move along the allowed transitions, and every record carries the
/// workspace it belongs to.
///
/// <para>
/// Two things it deliberately does not do. It publishes nothing to the outbox:
/// the events it describes happened days ago, and replaying them would send a
/// burst of e-mail about work that is already finished. And it uploads no
/// attachments, which would make seeding depend on blob storage being reachable
/// — the ticket a file hangs from is the interesting part, and that is here.
/// </para>
///
/// <para>
/// Timestamps are relative to the moment it runs. A fixed date would produce a
/// dashboard that is honest on the day it was written and wrong a week later:
/// "geciken" and "bu hafta" are questions about today (docs/PRODUCT.md).
/// </para>
/// </remarks>
public sealed class DemoDataSeeder
{
    private const string SupportSlug = "aydin-yazilim";
    private const string LogisticsSlug = "marmara-lojistik";

    private readonly FlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly IClock _clock;

    public DemoDataSeeder(
        FlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _clock = clock;
    }

    /// <summary>The workspaces the seed creates, for anyone reporting on it.</summary>
    public static IReadOnlyList<string> WorkspaceSlugs => [SupportSlug, LogisticsSlug];

    public async Task<DemoDataSeedResult> SeedAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        /*
          Refuses rather than adds. Running it twice would double every
          workspace name and leave two owners' worth of half-matching data,
          which is harder to recognise than an empty database.
        */
        var existing = await _dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Slug == SupportSlug || tenant.Slug == LogisticsSlug)
            .Select(tenant => tenant.Slug)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            return DemoDataSeedResult.AlreadyPresent(existing);
        }

        return await _dbContext.ExecuteInTransactionAsync(
            token => WriteAsync(password, token),
            cancellationToken);
    }

    private async Task<DemoDataSeedResult> WriteAsync(string password, CancellationToken token)
    {
        var now = _clock.UtcNow;
        var people = await CreateAccountsAsync(password, token);

        await CreateSupportWorkspaceAsync(people, now, token);
        await CreateLogisticsWorkspaceAsync(people, now, token);

        await _dbContext.SaveChangesAsync(token);

        /*
          Counted from what was actually written rather than from numbers kept
          beside the data. A tally maintained by hand is wrong the first time
          somebody adds a customer and does not notice the number below it.
        */
        return DemoDataSeedResult.Seeded(
            people.All.Count,
            _dbContext.ChangeTracker.Entries<Customer>().Count(),
            _dbContext.ChangeTracker.Entries<Ticket>().Count(),
            _dbContext.ChangeTracker.Entries<TaskItem>().Count(),
            people.Elif.Email);
    }

    // ---------------------------------------------------------------- accounts

    /// <summary>
    /// Creates the accounts, every one of them at a reserved address.
    /// </summary>
    /// <remarks>
    /// <c>.example</c> can never be registered (RFC 2606), so a stray
    /// notification from a demo cannot reach a real person's inbox, and nothing
    /// here can be mistaken for somebody's actual address (CLAUDE.md).
    /// </remarks>
    private async Task<DemoPeople> CreateAccountsAsync(string password, CancellationToken token)
    {
        var elif = await CreateAccountAsync("elif.demir@flowdesk.example", "Elif Demir", password, token);
        var burak = await CreateAccountAsync("burak.sahin@flowdesk.example", "Burak Şahin", password, token);
        var ceren = await CreateAccountAsync("ceren.yilmaz@flowdesk.example", "Ceren Yılmaz", password, token);
        var deniz = await CreateAccountAsync("deniz.kara@flowdesk.example", "Deniz Kara", password, token);
        var merve = await CreateAccountAsync("merve.aksoy@flowdesk.example", "Merve Aksoy", password, token);

        return new DemoPeople(elif, burak, ceren, deniz, merve);
    }

    private async Task<UserAccount> CreateAccountAsync(
        string email,
        string displayName,
        string password,
        CancellationToken token)
    {
        var result = await _accountStore.CreateAsync(email, displayName, password, token);

        if (result.IsFailure)
        {
            // The caller cannot fix this by trying again: either the password
            // fails the policy or the address is already taken. Say which.
            throw new InvalidOperationException(
                $"Demo hesabı oluşturulamadı ({email}): {result.Error.Message}");
        }

        return result.Value;
    }

    // --------------------------------------------------------------- workspaces

    private async Task CreateSupportWorkspaceAsync(
        DemoPeople people,
        DateTimeOffset now,
        CancellationToken token)
    {
        var tenant = AddWorkspace("Aydın Yazılım", SupportSlug, now.AddDays(-180));

        AddMember(tenant.Id, people.Elif.Id, MembershipRole.Owner, now.AddDays(-180));
        AddMember(tenant.Id, people.Burak.Id, MembershipRole.Admin, now.AddDays(-164));
        AddMember(tenant.Id, people.Ceren.Id, MembershipRole.Agent, now.AddDays(-151));
        AddMember(tenant.Id, people.Deniz.Id, MembershipRole.Agent, now.AddDays(-96));
        AddMember(tenant.Id, people.Merve.Id, MembershipRole.Viewer, now.AddDays(-42));

        var yildiz = AddCustomer(tenant.Id, people.Elif.Id, now.AddDays(-176), new CustomerDetails(
            "Yıldız Tekstil",
            "iletisim@yildiztekstil.example",
            "+90 212 555 01 42",
            "Yıldız Tekstil A.Ş.",
            CustomerStatus.Active,
            "Üretim planlama modülünü kullanıyor. Fatura dönemi ayın 15'i."));

        var ova = AddCustomer(tenant.Id, people.Elif.Id, now.AddDays(-158), new CustomerDetails(
            "Ova Gıda",
            "destek@ovagida.example",
            "+90 232 555 01 88",
            "Ova Gıda Sanayi Ltd. Şti.",
            CustomerStatus.Active,
            "Soğuk zincir raporlarını haftalık olarak istiyor."));

        var pusula = AddCustomer(tenant.Id, people.Burak.Id, now.AddDays(-133), new CustomerDetails(
            "Pusula Eğitim",
            "bilgi@pusulaegitim.example",
            "+90 312 555 01 19",
            "Pusula Eğitim Kurumları",
            CustomerStatus.Active,
            "Dönem başında eşzamanlı kullanıcı sayısı ikiye katlanıyor."));

        var kiyi = AddCustomer(tenant.Id, people.Ceren.Id, now.AddDays(-97), new CustomerDetails(
            "Kıyı Mimarlık",
            "ofis@kiyimimarlik.example",
            "+90 242 555 01 73",
            "Kıyı Mimarlık ve Tasarım",
            CustomerStatus.Active,
            "Proje dosyalarını büyük boyutta yüklüyor; ek boyutu sınırına takılıyorlar."));

        var tuna = AddCustomer(tenant.Id, people.Burak.Id, now.AddDays(-71), new CustomerDetails(
            "Tuna Makine",
            "servis@tunamakine.example",
            "+90 224 555 01 64",
            "Tuna Makine San. ve Tic.",
            CustomerStatus.Active,
            "Saha ekibi uygulamaya mobil tarayıcıdan bağlanıyor."));

        var beyaz = AddCustomer(tenant.Id, people.Elif.Id, now.AddDays(-38), new CustomerDetails(
            "Beyaz Adım Danışmanlık",
            "merhaba@beyazadim.example",
            "+90 216 555 01 27",
            "Beyaz Adım Yönetim Danışmanlığı",
            CustomerStatus.Active,
            "Deneme sürecinde. Sözleşme görüşmesi sürüyor."));

        var ege = AddCustomer(tenant.Id, people.Burak.Id, now.AddDays(-120), new CustomerDetails(
            "Ege Lojistik",
            "operasyon@egelojistik.example",
            "+90 232 555 01 55",
            "Ege Lojistik Hizmetleri",
            CustomerStatus.Inactive,
            "Sözleşme askıda. Yenileme görüşmesi ilk çeyrekte."));

        // Archived, so the customer list has something to hide by default and
        // the "arşivlenmiş" filter has something to show.
        var kabuk = AddCustomer(tenant.Id, people.Ceren.Id, now.AddDays(-165), new CustomerDetails(
            "Deniz Kabuğu Turizm",
            "rezervasyon@denizkabugu.example",
            "+90 252 555 01 36",
            "Deniz Kabuğu Turizm Acentesi",
            CustomerStatus.Inactive,
            "Sezonluk müşteri. Kasımda kapandı."));

        kabuk.Archive(now.AddDays(-34));
        AddActivity(
            tenant.Id,
            people.Ceren.Id,
            ActivityType.CustomerArchived,
            ActivitySubject.Customer,
            kabuk.Id,
            new CustomerActivityPayload(kabuk.Name),
            now.AddDays(-34));

        var tickets = new[]
        {
            new TicketPlan(
                yildiz.Id,
                "Aylık fatura PDF'i e-postayla ulaşmıyor",
                "Müşteri, ayın 15'inde otomatik gönderilen fatura özetini üç aydır alamadığını bildirdi. "
                + "Panelden indirildiğinde dosya sorunsuz açılıyor; sorun yalnızca e-posta gönderiminde.",
                TicketPriority.High,
                people.Elif.Id,
                people.Ceren.Id,
                CreatedDaysAgo: 24,
                [new StatusStep(TicketStatus.InProgress, 23), new StatusStep(TicketStatus.Resolved, 19)],
                [
                    new CommentPlan(people.Ceren.Id, "Gönderim kayıtlarına baktım: adres doğru ama sağlayıcı iletileri geri çeviriyor. Alan adı kayıtlarını kontrol etmelerini istedim.", 23),
                    new CommentPlan(people.Elif.Id, "Müşteriyi aradım, bilgi işlemleri SPF kaydını güncelleyecek.", 21),
                    new CommentPlan(people.Ceren.Id, "Güncelleme sonrası test gönderimi ulaştı. Müşteri onayladı, kapatıyorum.", 19),
                ]),

            new TicketPlan(
                ova.Id,
                "Soğuk zincir raporunda sıcaklık sütunu boş geliyor",
                "Haftalık raporun son iki kopyasında sıcaklık sütunu boş. Sensör verisi panelde görünüyor, "
                + "yalnızca dışa aktarılan dosyada eksik.",
                TicketPriority.Urgent,
                people.Burak.Id,
                people.Deniz.Id,
                CreatedDaysAgo: 6,
                [new StatusStep(TicketStatus.InProgress, 5)],
                [
                    new CommentPlan(people.Deniz.Id, "Dışa aktarma şablonunda sütun adı değişmiş. Düzeltmeyi hazırlıyorum.", 5),
                    new CommentPlan(people.Burak.Id, "Müşteriye bugün içinde dönüş yapacağımızı ilettim.", 4),
                ]),

            new TicketPlan(
                pusula.Id,
                "Dönem başında giriş ekranı yavaşlıyor",
                "Kayıt haftasında eşzamanlı kullanıcı sayısı artınca giriş ekranı 8-10 saniyede açılıyor. "
                + "Diğer sayfalarda yavaşlama bildirilmedi.",
                TicketPriority.High,
                people.Elif.Id,
                people.Burak.Id,
                CreatedDaysAgo: 11,
                [new StatusStep(TicketStatus.InProgress, 10), new StatusStep(TicketStatus.Waiting, 7)],
                [
                    new CommentPlan(people.Burak.Id, "Ölçüm aldım: gecikme oturum doğrulamasında. Önbellek ayarlarını gözden geçiriyorum.", 10),
                    new CommentPlan(people.Burak.Id, "Müşteriden kayıt haftasının tam tarih aralığını bekliyorum.", 7),
                ]),

            new TicketPlan(
                kiyi.Id,
                "25 MB üzerindeki proje dosyaları yüklenemiyor",
                "Mimari çizimler sıkıştırılmış hâlde bile sınırın üzerinde kalıyor. Müşteri sınırın "
                + "yükseltilmesini veya bir bağlantı paylaşım yöntemi istiyor.",
                TicketPriority.Medium,
                people.Ceren.Id,
                null,
                CreatedDaysAgo: 4,
                [],
                [new CommentPlan(people.Ceren.Id, "Ürün tarafına sordum; sınır bilinçli. Alternatif bir akış önerebilir miyiz, araştırıyorum.", 3)]),

            new TicketPlan(
                tuna.Id,
                "Mobil tarayıcıda görev listesi kayıyor",
                "Saha ekibi telefondan bağlandığında görev listesi yatayda taşıyor ve son sütun okunamıyor.",
                TicketPriority.Medium,
                people.Deniz.Id,
                people.Deniz.Id,
                CreatedDaysAgo: 9,
                [new StatusStep(TicketStatus.InProgress, 8), new StatusStep(TicketStatus.Resolved, 5), new StatusStep(TicketStatus.Closed, 3)],
                [
                    new CommentPlan(people.Deniz.Id, "Dar ekranda tablo yerine kart düzenine geçtik. Test cihazında doğruladım.", 5),
                ]),

            new TicketPlan(
                yildiz.Id,
                "Üretim planında hafta sonu vardiyası görünmüyor",
                "Cumartesi vardiyası planlandığı hâlde haftalık görünümde yer almıyor. Pazartesi görünümünde sorun yok.",
                TicketPriority.High,
                people.Burak.Id,
                people.Ceren.Id,
                CreatedDaysAgo: 2,
                [],
                []),

            new TicketPlan(
                beyaz.Id,
                "Deneme hesabında kullanıcı davet edilemiyor",
                "Deneme sürecindeki müşteri ikinci kullanıcıyı davet etmek istiyor, davet ekranı hata veriyor.",
                TicketPriority.Urgent,
                people.Elif.Id,
                null,
                CreatedDaysAgo: 1,
                [],
                []),

            new TicketPlan(
                pusula.Id,
                "Öğrenci listesi dışa aktarımında Türkçe karakter bozuluyor",
                "Dışa aktarılan dosya Excel'de açıldığında ş, ğ ve ı harfleri bozuk görünüyor.",
                TicketPriority.Low,
                people.Ceren.Id,
                people.Ceren.Id,
                CreatedDaysAgo: 31,
                [new StatusStep(TicketStatus.InProgress, 30), new StatusStep(TicketStatus.Resolved, 28), new StatusStep(TicketStatus.Closed, 26)],
                [new CommentPlan(people.Ceren.Id, "Dosya artık BOM ile yazılıyor; Excel doğru açıyor.", 28)]),

            new TicketPlan(
                ova.Id,
                "Depo girişlerinde çift kayıt oluşuyor",
                "Barkod okuyucu hızlı okutulduğunda aynı giriş iki kez kaydediliyor.",
                TicketPriority.High,
                people.Deniz.Id,
                people.Burak.Id,
                CreatedDaysAgo: 18,
                [new StatusStep(TicketStatus.InProgress, 17), new StatusStep(TicketStatus.Resolved, 13), new StatusStep(TicketStatus.Closed, 11)],
                [
                    new CommentPlan(people.Burak.Id, "Aynı barkod için kısa aralıkta gelen ikinci isteği yok sayıyoruz.", 15),
                    new CommentPlan(people.Deniz.Id, "Depo ekibi bir haftadır sorun yaşamadığını bildirdi.", 13),
                ]),

            new TicketPlan(
                tuna.Id,
                "Servis formunda müşteri imzası kaydedilmiyor",
                "Saha teknisyeni imzayı alıyor, forma dönüldüğünde imza alanı boş.",
                TicketPriority.Urgent,
                people.Ceren.Id,
                people.Deniz.Id,
                CreatedDaysAgo: 13,
                [new StatusStep(TicketStatus.InProgress, 12), new StatusStep(TicketStatus.Waiting, 9)],
                [new CommentPlan(people.Deniz.Id, "Müşteriden cihaz modeli ve tarayıcı sürümü bekliyorum.", 9)]),

            new TicketPlan(
                kiyi.Id,
                "Fatura adresi güncellenemiyor",
                "Müşteri taşındı, yeni adres kaydedilmiyor; kaydet düğmesi tepki vermiyor.",
                TicketPriority.Medium,
                people.Elif.Id,
                people.Burak.Id,
                CreatedDaysAgo: 40,
                [new StatusStep(TicketStatus.InProgress, 39), new StatusStep(TicketStatus.Resolved, 36), new StatusStep(TicketStatus.Closed, 34)],
                []),

            new TicketPlan(
                ege.Id,
                "Sözleşme askıya alınmadan önceki veriler ne olacak?",
                "Müşteri, sözleşme yenilenene kadar verilerinin saklanıp saklanmayacağını soruyor.",
                TicketPriority.Low,
                people.Burak.Id,
                people.Elif.Id,
                CreatedDaysAgo: 27,
                [new StatusStep(TicketStatus.Waiting, 26)],
                [new CommentPlan(people.Elif.Id, "Saklama süresini sözleşme ekibine sordum, yazılı yanıt bekliyorum.", 26)]),

            new TicketPlan(
                yildiz.Id,
                "Yeni vardiya sorumlusu için yetki tanımı",
                "Vardiya sorumlusunun yalnızca kendi hattını görmesi isteniyor.",
                TicketPriority.Low,
                people.Ceren.Id,
                null,
                CreatedDaysAgo: 7,
                [],
                [new CommentPlan(people.Elif.Id, "Bu, rol matrisinde olmayan bir kırılım. Ürün tarafına not düştüm.", 6)]),

            new TicketPlan(
                pusula.Id,
                "Dönem sonu raporu iki kez gönderildi",
                "Aynı rapor aynı gün iki kez e-postayla ulaştı.",
                TicketPriority.Medium,
                people.Deniz.Id,
                people.Ceren.Id,
                CreatedDaysAgo: 46,
                [new StatusStep(TicketStatus.InProgress, 45), new StatusStep(TicketStatus.Resolved, 43), new StatusStep(TicketStatus.Closed, 41)],
                [new CommentPlan(people.Ceren.Id, "Zamanlanmış iş iki kez tetiklenmiş. Tekrarı engelleyen kontrol eklendi.", 43)]),
        };

        /*
          Saved before the tickets, because the ticket number comes from a row
          the counter locks with raw SQL — and raw SQL cannot see what is still
          only in the change tracker. Inside the transaction either way, so a
          later failure still takes this with it.
        */
        await _dbContext.SaveChangesAsync(token);

        var created = new List<Ticket>(tickets.Length);

        foreach (var plan in tickets)
        {
            created.Add(await AddTicketAsync(tenant.Id, plan, now, token));
        }

        AddSupportTasks(tenant.Id, people, yildiz.Id, ova.Id, pusula.Id, kiyi.Id, tuna.Id, now);
        AddSupportNotifications(tenant.Id, SupportSlug, people, created, now);
    }

    /// <summary>
    /// A second workspace, so multi-tenancy is something the reader can see
    /// rather than take on trust.
    /// </summary>
    /// <remarks>
    /// Deniz owns this one and is only an agent in the first; Elif owns the
    /// first and is an admin here. The same account, two workspaces, two sets
    /// of permissions — which is the whole point of the membership model
    /// (ADR-0003).
    /// </remarks>
    private async Task CreateLogisticsWorkspaceAsync(
        DemoPeople people,
        DateTimeOffset now,
        CancellationToken token)
    {
        var tenant = AddWorkspace("Marmara Lojistik", LogisticsSlug, now.AddDays(-88));

        AddMember(tenant.Id, people.Deniz.Id, MembershipRole.Owner, now.AddDays(-88));
        AddMember(tenant.Id, people.Elif.Id, MembershipRole.Admin, now.AddDays(-80));

        var kuzey = AddCustomer(tenant.Id, people.Deniz.Id, now.AddDays(-84), new CustomerDetails(
            "Kuzey Nakliyat",
            "planlama@kuzeynakliyat.example",
            "+90 262 555 02 11",
            "Kuzey Nakliyat ve Dağıtım",
            CustomerStatus.Active,
            "Gece sevkiyatlarını takip ediyor."));

        var akdeniz = AddCustomer(tenant.Id, people.Elif.Id, now.AddDays(-60), new CustomerDetails(
            "Akdeniz Soğuk Zincir",
            "operasyon@akdenizsoguk.example",
            "+90 324 555 02 48",
            "Akdeniz Soğuk Zincir Lojistik",
            CustomerStatus.Active,
            "Araç sıcaklık kayıtlarını denetime sunuyor."));

        var tickets = new[]
        {
            new TicketPlan(
                kuzey.Id,
                "Gece sevkiyatı raporu sabah 06:00'da gelmiyor",
                "Rapor otomatik olarak 06:00'da bekleniyor, son iki haftadır 09:00'u buluyor.",
                TicketPriority.High,
                people.Deniz.Id,
                people.Elif.Id,
                CreatedDaysAgo: 5,
                [new StatusStep(TicketStatus.InProgress, 4)],
                [new CommentPlan(people.Elif.Id, "Zamanlanmış iş sırası değişmiş; yeniden düzenliyorum.", 4)]),

            new TicketPlan(
                akdeniz.Id,
                "Araç sıcaklık kaydı denetim formatında istenmiş",
                "Denetim kurumu kayıtları imzalı PDF olarak istiyor.",
                TicketPriority.Medium,
                people.Elif.Id,
                null,
                CreatedDaysAgo: 2,
                [],
                []),
        };

        // As above: the counter row is locked with raw SQL, so the workspace
        // and its customers have to exist in the database first.
        await _dbContext.SaveChangesAsync(token);

        foreach (var plan in tickets)
        {
            await AddTicketAsync(tenant.Id, plan, now, token);
        }

        AddTask(
                tenant.Id,
                people.Deniz.Id,
                "Kuzey Nakliyat ile üç aylık değerlendirme",
                "Gece sevkiyatı raporlarının kapsamı gözden geçirilecek.",
                kuzey.Id,
                people.Elif.Id,
                dueAt: now.AddDays(4),
                createdAt: now.AddDays(-9),
                TaskItemStatus.Todo);

        AddTask(
                tenant.Id,
                people.Elif.Id,
                "Sıcaklık kaydı dışa aktarımını denetime hazırla",
                "Akdeniz Soğuk Zincir denetim için son altı ayı istiyor.",
                akdeniz.Id,
                people.Deniz.Id,
                dueAt: now.AddDays(-2),
                createdAt: now.AddDays(-15),
                TaskItemStatus.InProgress);
    }

    // ------------------------------------------------------------------ pieces

    private Tenant AddWorkspace(string name, string slug, DateTimeOffset createdAt)
    {
        if (!WorkspaceSlug.TryCreate(slug, out var workspaceSlug) || workspaceSlug is null)
        {
            throw new InvalidOperationException($"Demo çalışma alanı adresi geçersiz: {slug}");
        }

        var tenant = Tenant.Create(name, workspaceSlug, createdAt);
        _dbContext.Tenants.Add(tenant);

        return tenant;
    }

    private void AddMember(Guid tenantId, Guid userId, MembershipRole role, DateTimeOffset joinedAt)
    {
        _dbContext.Memberships.Add(Membership.Create(userId, tenantId, role, joinedAt));

        AddActivity(
            tenantId,
            userId,
            ActivityType.MemberJoined,
            ActivitySubject.Member,
            userId,
            new MemberActivityPayload(userId, null, role),
            joinedAt);
    }

    private Customer AddCustomer(
        Guid tenantId,
        Guid actorUserId,
        DateTimeOffset createdAt,
        CustomerDetails details)
    {
        var customer = Customer.Create(tenantId, details, createdAt);
        _dbContext.Customers.Add(customer);

        AddActivity(
            tenantId,
            actorUserId,
            ActivityType.CustomerCreated,
            ActivitySubject.Customer,
            customer.Id,
            new CustomerActivityPayload(customer.Name),
            createdAt);

        return customer;
    }

    private async Task<Ticket> AddTicketAsync(
        Guid tenantId,
        TicketPlan plan,
        DateTimeOffset now,
        CancellationToken token)
    {
        // The same counter the API uses, so the demo's numbering is the
        // product's numbering rather than a parallel invention.
        var number = await _dbContext.TakeNextTicketNumberAsync(tenantId, token);
        var createdAt = now.AddDays(-plan.CreatedDaysAgo);

        var ticket = Ticket.Create(
            tenantId,
            number,
            plan.CustomerId,
            plan.Subject,
            plan.Description,
            plan.Priority,
            plan.CreatedByUserId,
            plan.AssignedUserId,
            createdAt);

        _dbContext.Tickets.Add(ticket);

        AddActivity(
            tenantId,
            plan.CreatedByUserId,
            ActivityType.TicketCreated,
            ActivitySubject.Ticket,
            ticket.Id,
            new TicketActivityPayload(ticket.Number, ticket.Subject),
            createdAt);

        if (plan.AssignedUserId is { } assignedUserId)
        {
            AddActivity(
                tenantId,
                plan.CreatedByUserId,
                ActivityType.TicketAssigned,
                ActivitySubject.Ticket,
                ticket.Id,
                new TicketAssignmentActivityPayload(ticket.Number, ticket.Subject, assignedUserId),
                createdAt);
        }

        var previous = ticket.Status;

        foreach (var step in plan.Steps)
        {
            var occurredAt = now.AddDays(-step.DaysAgo);

            // Goes through the entity, so an invalid demo transition fails here
            // rather than producing a ticket the product could never reach.
            ticket.ChangeStatus(step.Status, occurredAt);

            AddActivity(
                tenantId,
                plan.AssignedUserId ?? plan.CreatedByUserId,
                ActivityType.TicketStatusChanged,
                ActivitySubject.Ticket,
                ticket.Id,
                new TicketStatusActivityPayload(ticket.Number, ticket.Subject, previous, step.Status),
                occurredAt);

            previous = step.Status;
        }

        foreach (var comment in plan.Comments)
        {
            var occurredAt = now.AddDays(-comment.DaysAgo);

            _dbContext.TicketComments.Add(TicketComment.Create(
                tenantId, ticket.Id, comment.AuthorUserId, comment.Body, occurredAt));

            AddActivity(
                tenantId,
                comment.AuthorUserId,
                ActivityType.TicketCommented,
                ActivitySubject.Ticket,
                ticket.Id,
                new TicketActivityPayload(ticket.Number, ticket.Subject),
                occurredAt);
        }

        return ticket;
    }

    /// <summary>
    /// Tasks spread across the three questions the dashboard asks: what is
    /// late, what is due this week, and what is finished.
    /// </summary>
    private void AddSupportTasks(
        Guid tenantId,
        DemoPeople people,
        Guid yildizId,
        Guid ovaId,
        Guid pusulaId,
        Guid kiyiId,
        Guid tunaId,
        DateTimeOffset now)
    {
        // Late.
        AddTask(tenantId, people.Elif.Id, "Yıldız Tekstil ile fatura akışını gözden geçir",
            "E-posta gönderimi düzeldikten sonra süreç birlikte teyit edilecek.",
            yildizId, people.Ceren.Id, now.AddDays(-3), now.AddDays(-17), TaskItemStatus.InProgress);

        AddTask(tenantId, people.Burak.Id, "Ova Gıda rapor şablonunu güncelle",
            "Sıcaklık sütunu düzeltmesi şablona işlenecek.",
            ovaId, people.Deniz.Id, now.AddDays(-1), now.AddDays(-8), TaskItemStatus.Todo);

        // Due this week.
        AddTask(tenantId, people.Elif.Id, "Pusula Eğitim için kapasite planı çıkar",
            "Kayıt haftasında beklenen eşzamanlı kullanıcı sayısı hesaplanacak.",
            pusulaId, people.Burak.Id, now.AddDays(2), now.AddDays(-6), TaskItemStatus.InProgress);

        AddTask(tenantId, people.Ceren.Id, "Kıyı Mimarlık'a dosya boyutu için alternatif öner",
            "Büyük çizimler için paylaşım akışı araştırılacak.",
            kiyiId, people.Ceren.Id, now.AddDays(5), now.AddDays(-4), TaskItemStatus.Todo);

        AddTask(tenantId, people.Burak.Id, "Haftalık destek özetini hazırla",
            null, null, people.Elif.Id, now.AddDays(1), now.AddDays(-2), TaskItemStatus.Todo);

        // Later, and one with no date at all.
        AddTask(tenantId, people.Deniz.Id, "Tuna Makine saha ekibiyle kısa eğitim",
            "Mobil görünüm değişikliği anlatılacak.",
            tunaId, people.Deniz.Id, now.AddDays(12), now.AddDays(-5), TaskItemStatus.Todo);

        AddTask(tenantId, people.Elif.Id, "Bilgi tabanı yazılarını gözden geçir",
            "Sıralama ve başlıklar güncellenecek.",
            null, null, null, now.AddDays(-21), TaskItemStatus.Todo);

        // Finished.
        AddTask(tenantId, people.Ceren.Id, "Türkçe karakter düzeltmesini müşteriye bildir",
            null, pusulaId, people.Ceren.Id, now.AddDays(-25), now.AddDays(-29), TaskItemStatus.Done);

        AddTask(tenantId, people.Burak.Id, "Depo çift kayıt düzeltmesini yayına al",
            null, ovaId, people.Burak.Id, now.AddDays(-14), now.AddDays(-19), TaskItemStatus.Done);
    }

    private TaskItem AddTask(
        Guid tenantId,
        Guid createdByUserId,
        string title,
        string? description,
        Guid? customerId,
        Guid? assignedUserId,
        DateTimeOffset? dueAt,
        DateTimeOffset createdAt,
        TaskItemStatus status)
    {
        var task = TaskItem.Create(
            tenantId, title, description, customerId, assignedUserId, dueAt, createdByUserId, createdAt);

        _dbContext.Tasks.Add(task);

        AddActivity(
            tenantId,
            createdByUserId,
            ActivityType.TaskCreated,
            ActivitySubject.TaskItem,
            task.Id,
            new TaskActivityPayload(task.Title, TaskItemStatus.Todo),
            createdAt);

        if (status is not TaskItemStatus.Todo)
        {
            // A day after it was raised, so the history reads in order rather
            // than showing the task created and finished in the same instant.
            var changedAt = createdAt.AddDays(1);

            task.ChangeStatus(status, changedAt);

            if (status is TaskItemStatus.Done)
            {
                AddActivity(
                    tenantId,
                    assignedUserId ?? createdByUserId,
                    ActivityType.TaskCompleted,
                    ActivitySubject.TaskItem,
                    task.Id,
                    new TaskActivityPayload(task.Title, TaskItemStatus.Done),
                    changedAt);
            }
        }

        return task;
    }

    /// <summary>
    /// A few unread notifications, so the bell is not empty on first sign-in.
    /// </summary>
    /// <remarks>
    /// Written directly rather than through the outbox. These describe work
    /// that happened days ago; publishing them would mean the worker sends a
    /// burst of e-mail about tickets that are already closed.
    /// </remarks>
    private void AddSupportNotifications(
        Guid tenantId,
        string slug,
        DemoPeople people,
        IReadOnlyList<Ticket> tickets,
        DateTimeOffset now)
    {
        var coldChain = Find(tickets, "Soğuk zincir");
        var weekendShift = Find(tickets, "Üretim planında");
        var signature = Find(tickets, "Servis formunda");

        AddNotification(tenantId, people.Deniz.Id, NotificationType.TicketAssigned, now.AddDays(-6),
            new TicketAssignedPayload(coldChain.Id, coldChain.Number, coldChain.Subject, people.Burak.DisplayName, slug));

        AddNotification(tenantId, people.Ceren.Id, NotificationType.TicketAssigned, now.AddDays(-2),
            new TicketAssignedPayload(weekendShift.Id, weekendShift.Number, weekendShift.Subject, people.Burak.DisplayName, slug));

        AddNotification(tenantId, people.Ceren.Id, NotificationType.TicketCommented, now.AddDays(-9),
            new TicketCommentedPayload(
                signature.Id,
                signature.Number,
                signature.Subject,
                people.Deniz.DisplayName,
                "Müşteriden cihaz modeli ve tarayıcı sürümü bekliyorum.",
                slug));
    }

    /// <summary>
    /// The ticket a notification is about, found by the start of its subject.
    /// </summary>
    /// <remarks>
    /// Says what it was looking for when it fails. Without this the seed would
    /// stop on "Sequence contains no matching element" the first time somebody
    /// reworded a subject, which names neither the subject nor this method.
    /// </remarks>
    private static Ticket Find(IReadOnlyList<Ticket> tickets, string subjectPrefix) =>
        tickets.FirstOrDefault(ticket =>
            ticket.Subject.StartsWith(subjectPrefix, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Bildirim için talep bulunamadı: konusu '{subjectPrefix}' ile başlayan bir demo talebi yok.");

    private void AddNotification(
        Guid tenantId,
        Guid userId,
        NotificationType type,
        DateTimeOffset createdAt,
        object payload) =>
        _dbContext.Notifications.Add(Notification.Create(
            tenantId,
            userId,
            type,
            JsonSerializer.Serialize(payload, payload.GetType(), FlowDeskMessageJson.Options),
            createdAt));

    private void AddActivity(
        Guid tenantId,
        Guid? actorUserId,
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        object payload,
        DateTimeOffset occurredAt) =>
        _dbContext.ActivityEvents.Add(ActivityEvent.Record(
            tenantId,
            actorUserId,
            type,
            subjectType,
            subjectId,
            JsonSerializer.Serialize(payload, payload.GetType(), FlowDeskMessageJson.Options),
            occurredAt));

    // ------------------------------------------------------------------ shapes

    private sealed record DemoPeople(
        UserAccount Elif,
        UserAccount Burak,
        UserAccount Ceren,
        UserAccount Deniz,
        UserAccount Merve)
    {
        public IReadOnlyList<UserAccount> All => [Elif, Burak, Ceren, Deniz, Merve];
    }

    /// <param name="CreatedDaysAgo">Days before the moment the seed runs.</param>
    private sealed record TicketPlan(
        Guid CustomerId,
        string Subject,
        string Description,
        TicketPriority Priority,
        Guid CreatedByUserId,
        Guid? AssignedUserId,
        double CreatedDaysAgo,
        IReadOnlyList<StatusStep> Steps,
        IReadOnlyList<CommentPlan> Comments);

    private sealed record StatusStep(TicketStatus Status, double DaysAgo);

    private sealed record CommentPlan(Guid AuthorUserId, string Body, double DaysAgo);
}
