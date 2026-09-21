using System.Net;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.Infrastructure.DemoData;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.DemoData;

/// <summary>
/// What the demo seed actually writes (Faz 21).
/// </summary>
/// <remarks>
/// Run against a database of its own. The seed uses fixed workspace addresses,
/// so sharing the suite's database would mean the first class to run decides
/// whether this one can write anything.
///
/// <para>
/// These are not assertions about how the demo reads. They are the things that
/// would make it useless or wrong: a workspace whose records belong to another
/// workspace, a dashboard with nothing on it, a burst of e-mail about work that
/// finished a month ago, or an account nobody can sign in to.
/// </para>
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class DemoDataSeederTests : IAsyncLifetime
{
    private const string SeedPassword = "DemoSeedParolasi2026";
    private const string SupportSlug = "aydin-yazilim";
    private const string LogisticsSlug = "marmara-lojistik";
    private const string OwnerEmail = "elif.demir@flowdesk.example";

    private readonly PostgresContainerFixture _postgres;

    private string _connectionString = string.Empty;
    private FlowDeskApiFactory? _factory;
    private WebApplicationFactory<Program>? _host;
    private int _exitCode = -1;

    public DemoDataSeederTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _postgres.CreateIsolatedDatabaseAsync(Cancellation);
        _factory = new FlowDeskApiFactory(_connectionString);

        // The password is supplied rather than left to the development
        // default, so the test proves the configured value is the one used.
        _host = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting(DemoDataPassword.SettingName, SeedPassword));

        /*
          Seeded once for the whole class, through the same entry point the
          command line uses. Calling the seeder directly would leave the part
          that decides the password and reports the outcome untested.
        */
        _exitCode = await _host.Services.RunFlowDeskDemoDataSeedAsync(Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Seed_iki_calisma_alani_ve_ekibi_olusturur()
    {
        Assert.Equal(0, _exitCode);

        await using var dbContext = OpenDbContext();

        var slugs = await dbContext.Tenants
            .AsNoTracking()
            .Select(tenant => tenant.Slug)
            .ToListAsync(Cancellation);

        Assert.Equal([SupportSlug, LogisticsSlug], slugs.Order());

        Assert.Equal(5, await dbContext.Users.AsNoTracking().CountAsync(Cancellation));
        Assert.Equal(7, await dbContext.Memberships.AsNoTracking().CountAsync(Cancellation));
    }

    [Fact]
    public async Task Ayni_hesap_iki_calisma_alaninda_farkli_rolde()
    {
        await using var dbContext = OpenDbContext();

        var support = await FindTenantAsync(dbContext, SupportSlug);
        var logistics = await FindTenantAsync(dbContext, LogisticsSlug);

        var owner = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(user => user.Email == OwnerEmail, Cancellation);

        var roles = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.UserId == owner.Id)
            .ToDictionaryAsync(membership => membership.TenantId, membership => membership.Role, Cancellation);

        // The point of a second workspace: the same person, two sets of
        // permissions (ADR-0003).
        Assert.Equal(MembershipRole.Owner, roles[support.Id]);
        Assert.Equal(MembershipRole.Admin, roles[logistics.Id]);
    }

    [Fact]
    public async Task Talep_numaralari_her_calisma_alaninda_birden_baslar()
    {
        await using var dbContext = OpenDbContext();

        var support = await FindTenantAsync(dbContext, SupportSlug);
        var logistics = await FindTenantAsync(dbContext, LogisticsSlug);

        var supportNumbers = await NumbersAsync(dbContext, support.Id);
        var logisticsNumbers = await NumbersAsync(dbContext, logistics.Id);

        /*
          Per workspace, unbroken, and starting where the product starts —
          TLP-1001, not TLP-1 (TicketNumber.FirstNumber). The seed takes its
          numbers from the same counter the API does rather than inventing them,
          so both workspaces begin again at the same place.
        */
        var first = TicketNumber.FirstNumber;

        Assert.Equal(Enumerable.Range(first, supportNumbers.Count), supportNumbers);
        Assert.Equal(Enumerable.Range(first, logisticsNumbers.Count), logisticsNumbers);
    }

    [Fact]
    public async Task Hicbir_kayit_yabanci_calisma_alanina_bagli_degil()
    {
        await using var dbContext = OpenDbContext();

        var customerTenants = await dbContext.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(customer => customer.Id, customer => customer.TenantId, Cancellation);

        var tickets = await dbContext.Tickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(ticket => new { ticket.Id, ticket.TenantId, ticket.CustomerId })
            .ToListAsync(Cancellation);

        // Tenant isolation is a security boundary, and demo data that crosses
        // it would be a working example of the bug (CLAUDE.md).
        foreach (var ticket in tickets)
        {
            Assert.Equal(ticket.TenantId, customerTenants[ticket.CustomerId]);
        }

        var tasks = await dbContext.Tasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(task => task.CustomerId != null)
            .Select(task => new { task.TenantId, task.CustomerId })
            .ToListAsync(Cancellation);

        foreach (var task in tasks)
        {
            Assert.Equal(task.TenantId, customerTenants[task.CustomerId!.Value]);
        }

        var ticketTenants = tickets.ToDictionary(ticket => ticket.Id, ticket => ticket.TenantId);

        var comments = await dbContext.TicketComments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(comment => new { comment.TenantId, comment.TicketId })
            .ToListAsync(Cancellation);

        Assert.NotEmpty(comments);

        foreach (var comment in comments)
        {
            Assert.Equal(comment.TenantId, ticketTenants[comment.TicketId]);
        }
    }

    [Fact]
    public async Task Panonun_sordugu_her_soru_icin_veri_var()
    {
        await using var dbContext = OpenDbContext();

        var now = DateTimeOffset.UtcNow;
        var support = await FindTenantAsync(dbContext, SupportSlug);

        var tickets = await dbContext.Tickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(ticket => ticket.TenantId == support.Id)
            .Select(ticket => new { ticket.Status, ticket.AssignedUserId })
            .ToListAsync(Cancellation);

        var tasks = await dbContext.Tasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(task => task.TenantId == support.Id)
            .Select(task => new { task.Status, task.DueAt })
            .ToListAsync(Cancellation);

        // A dashboard where every figure is zero demonstrates nothing.
        Assert.True(
            tickets.Select(ticket => ticket.Status).Distinct().Count() >= 4,
            "Talepler en az dört farklı durumda olmalı.");

        Assert.Contains(
            tickets,
            ticket => ticket.AssignedUserId is null
                && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed);

        Assert.Contains(
            tasks,
            task => task.Status is not TaskItemStatus.Done && task.DueAt is { } due && due < now);

        Assert.Contains(
            tasks,
            task => task.Status is not TaskItemStatus.Done
                && task.DueAt is { } due
                && due >= now
                && due <= now.AddDays(7));

        Assert.Contains(tasks, task => task.Status is TaskItemStatus.Done);

        // Something for the archive filter to show, and to hide by default.
        var archived = await dbContext.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(customer => customer.ArchivedAt != null, Cancellation);

        Assert.True(archived > 0);
    }

    [Fact]
    public async Task Gecmise_ait_isler_icin_e_posta_kuyruga_girmez()
    {
        await using var dbContext = OpenDbContext();

        var pending = await dbContext.OutboxMessages.AsNoTracking().CountAsync(Cancellation);

        /*
          The seed describes work that finished days ago. Publishing it would
          hand the worker a queue full of assignment and comment notifications,
          and whoever seeded would get a burst of mail about closed tickets.
        */
        Assert.Equal(0, pending);

        // The notifications themselves are written directly, so the bell is
        // not empty on first sign-in.
        var notifications = await dbContext.Notifications.IgnoreQueryFilters().AsNoTracking().CountAsync(Cancellation);

        Assert.True(notifications > 0);
    }

    [Fact]
    public async Task Etkinlik_akisi_gecmisi_anlatiyor()
    {
        await using var dbContext = OpenDbContext();

        var events = await dbContext.ActivityEvents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(activity => new { activity.Type, activity.OccurredAt, activity.Payload })
            .ToListAsync(Cancellation);

        Assert.True(events.Count > 50, "Etkinlik akışı boş görünmemeli.");

        // Everything already happened: a feed with tomorrow's events in it
        // would be the first thing a reader noticed.
        Assert.All(events, activity => Assert.True(activity.OccurredAt <= DateTimeOffset.UtcNow));
        Assert.All(events, activity => Assert.False(string.IsNullOrWhiteSpace(activity.Payload)));

        Assert.True(events.Select(activity => activity.Type).Distinct().Count() >= 5);
    }

    [Fact]
    public async Task Demo_hesabi_gercekten_giris_yapabiliyor()
    {
        using var client = _host!.CreateClient();

        using var response = await AuthTestClient.LoginAsync(
            client, OwnerEmail, SeedPassword, Cancellation);

        // The whole point of the phase: someone opens the project and gets in.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ikinci_calistirma_hicbir_sey_yazmaz()
    {
        await using var before = OpenDbContext();
        var ticketsBefore = await before.Tickets.IgnoreQueryFilters().AsNoTracking().CountAsync(Cancellation);
        var usersBefore = await before.Users.AsNoTracking().CountAsync(Cancellation);

        var second = await _host!.Services.RunFlowDeskDemoDataSeedAsync(Cancellation);

        // Not an error: finding the data already there is a normal outcome.
        Assert.Equal(0, second);

        await using var after = OpenDbContext();

        // Refusing is the behaviour under test: a second copy of every
        // workspace is harder to recognise than an empty database.
        Assert.Equal(ticketsBefore, await after.Tickets.IgnoreQueryFilters().AsNoTracking().CountAsync(Cancellation));
        Assert.Equal(usersBefore, await after.Users.AsNoTracking().CountAsync(Cancellation));
    }

    /// <summary>
    /// A context of this test's own, outside the application's container.
    /// </summary>
    /// <remarks>
    /// Every query through it says <c>IgnoreQueryFilters</c>. The workspace
    /// filter exists to answer "what can this request see", and these tests are
    /// asking the opposite question — what is in the database, across every
    /// workspace at once — so the filter is turned off deliberately rather than
    /// relied upon to be inert.
    /// </remarks>
    private FlowDeskDbContext OpenDbContext()
    {
        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new FlowDeskDbContext(options);
    }

    private static Task<Tenant> FindTenantAsync(FlowDeskDbContext dbContext, string slug) =>
        dbContext.Tenants.AsNoTracking().SingleAsync(tenant => tenant.Slug == slug, Cancellation);

    private static async Task<List<int>> NumbersAsync(FlowDeskDbContext dbContext, Guid tenantId)
    {
        var numbers = await dbContext.Tickets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(ticket => ticket.TenantId == tenantId)
            .Select(ticket => ticket.Number)
            .ToListAsync(Cancellation);

        numbers.Sort();

        return numbers;
    }
}
