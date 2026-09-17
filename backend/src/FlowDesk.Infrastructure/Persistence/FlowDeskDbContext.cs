using System.Reflection;
using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Authentication;
using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Messaging;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tickets;
using FlowDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Persistence;

/// <summary>
/// The application's EF Core context, backed by PostgreSQL.
/// </summary>
/// <remarks>
/// Implements <see cref="IFlowDeskDbContext"/> so the application layer can use
/// EF Core directly while the dependency arrow keeps pointing inwards
/// (ADR-0021). There is no repository layer on top: <c>DbSet&lt;T&gt;</c> is
/// already the query surface we want, and wrapping it would only cost
/// projections and query composition (ADR-0004).
///
/// Derives from <c>IdentityUserContext</c> rather than <c>IdentityDbContext</c>
/// so that no role tables are created. A user's role belongs to their
/// membership in a workspace, not to the account (ADR-0003), which would leave
/// <c>AspNetRoles</c>, <c>AspNetUserRoles</c> and <c>AspNetRoleClaims</c>
/// permanently empty.
/// </remarks>
public sealed class FlowDeskDbContext
    : IdentityUserContext<ApplicationUser, Guid>, IFlowDeskDbContext
{
    private readonly ITenantContext? _tenantContext;

    public FlowDeskDbContext(DbContextOptions<FlowDeskDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Used by the application host, where a workspace may be in scope.
    /// </summary>
    /// <remarks>
    /// The context is optional so that migrations and design-time tooling can
    /// build the model without a request to resolve a workspace from.
    /// </remarks>
    public FlowDeskDbContext(DbContextOptions<FlowDeskDbContext> options, ITenantContext tenantContext)
        : base(options) => _tenantContext = tenantContext;

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    /// <summary>
    /// Per-workspace ticket numbering. Not covered by the workspace query
    /// filter because it is keyed by workspace already and is only ever read
    /// with an explicit id and a row lock.
    /// </summary>
    public DbSet<TenantCounter> TenantCounters => Set<TenantCounter>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction.
    /// </summary>
    /// <remarks>
    /// Joins an ambient transaction rather than nesting a second one: a caller
    /// that already opened a transaction expects its work to land with the
    /// rest, not in a separate unit that could commit on its own.
    /// </remarks>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

        var result = await work(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Locks the workspace's counter row and returns the next ticket number.
    /// </summary>
    /// <remarks>
    /// The lock is what makes numbering safe under concurrency. Two requests
    /// creating a ticket at the same moment queue here; the second reads the
    /// value the first already advanced, so they cannot take the same number.
    /// The lock is released when the surrounding transaction ends.
    ///
    /// The counter row is created on first use rather than when the workspace
    /// is created, so a workspace that never raises a ticket costs nothing.
    /// Creating it is a separate statement for a reason given below.
    /// </remarks>
    public async Task<int> TakeNextTicketNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        /*
          Ensures the counter row exists before anything tries to lock it.

          FOR UPDATE can only lock a row that is already there. Without this
          step, the first two tickets ever raised in a workspace would both find
          no counter, both insert one, and the second would fail on the primary
          key — a 500 on the very first concurrent use, and exactly the case the
          lock was meant to cover.

          ON CONFLICT DO NOTHING makes the creation itself the serialisation
          point: whoever gets there second waits for the first to commit and
          then finds the row waiting for them.

          The starting state still comes from the domain, so where numbering
          begins is defined in one place.
        */
        var seed = TenantCounter.StartFor(tenantId);

        await Database.ExecuteSqlAsync(
            $"""
             INSERT INTO "TenantCounters" ("TenantId", "NextTicketNumber")
             VALUES ({seed.TenantId}, {seed.NextTicketNumber})
             ON CONFLICT ("TenantId") DO NOTHING
             """,
            cancellationToken);

        var counter = await TenantCounters
            .FromSql($"SELECT * FROM \"TenantCounters\" WHERE \"TenantId\" = {tenantId} FOR UPDATE")
            .FirstAsync(cancellationToken);

        var number = counter.TakeNextTicketNumber();

        await SaveChangesAsync(cancellationToken);

        return number;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyTenantQueryFilters(builder);
    }

    /// <summary>
    /// Scopes every <see cref="ITenantOwned"/> entity to the current workspace.
    /// </summary>
    /// <remarks>
    /// Applied by walking the model rather than entity by entity, so a new
    /// tenant-owned entity is enrolled by implementing the interface and the
    /// filter cannot be forgotten. Forgetting one would be a cross-workspace
    /// data leak, which is the failure mode this exists to prevent.
    ///
    /// This is the second of three defence layers and is explicitly not the
    /// boundary: <c>IgnoreQueryFilters</c> bypasses it and raw SQL never sees
    /// it. The boundary is the membership check performed on every request
    /// (docs/SECURITY.md).
    ///
    /// <see cref="Membership"/> is excluded. Listing the workspaces a user
    /// belongs to has to look across all of them, and scoping memberships to
    /// the current workspace would make that question unanswerable.
    /// </remarks>
    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (!typeof(ITenantOwned).IsAssignableFrom(clrType) || clrType == typeof(Membership))
            {
                continue;
            }

            // Customer carries the archive condition as well, so it gets its
            // own filter rather than the generic one.
            if (clrType == typeof(Customer))
            {
                ConfigureCustomerFilter(builder);
                continue;
            }

            ConfigureTenantFilterMethod
                .MakeGenericMethod(clrType)
                .Invoke(this, [builder]);
        }
    }

    private static readonly MethodInfo ConfigureTenantFilterMethod =
        typeof(FlowDeskDbContext).GetMethod(
            nameof(ConfigureTenantFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Applies the workspace filter to one entity type.
    /// </summary>
    /// <remarks>
    /// Written as a typed lambda rather than a hand-built expression tree
    /// because this exact shape — a filter reading a field on the DbContext —
    /// is what EF Core recognises and re-evaluates per query. The model itself
    /// is compiled once and cached across context instances, so a filter that
    /// captured a value instead of the context would freeze the first request's
    /// workspace into every later one.
    ///
    /// The unresolved cases keep the filter inert outside a workspace — during
    /// migrations, design-time model building and background work — instead of
    /// silently matching an empty id and returning nothing.
    /// </remarks>
    private void ConfigureTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantOwned =>
        builder.Entity<TEntity>().HasQueryFilter(entity =>
            _tenantContext == null
            || !_tenantContext.IsResolved
            || entity.TenantId == _tenantContext.TenantId);

    /// <summary>
    /// Hides archived customers from every query that does not ask for them.
    /// </summary>
    /// <remarks>
    /// Declared alongside the workspace filter rather than inside
    /// <c>CustomerConfiguration</c>, because EF applies one filter per entity
    /// and defining it in both places would silently replace the tenant scope —
    /// turning a soft-delete convenience into a cross-workspace leak.
    ///
    /// Customer is the only entity that is archived rather than deleted
    /// (ADR-0012), so this stays a named special case instead of a second
    /// generic mechanism.
    /// </remarks>
    private void ConfigureCustomerFilter(ModelBuilder builder) =>
        builder.Entity<Customer>().HasQueryFilter(customer =>
            (_tenantContext == null
                || !_tenantContext.IsResolved
                || customer.TenantId == _tenantContext.TenantId)
            && customer.ArchivedAt == null);
}
