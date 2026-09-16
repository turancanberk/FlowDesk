using System.Reflection;
using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Authentication;
using FlowDesk.Domain.Tenancy;
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
}
