using System.Reflection;
using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Authentication;
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
    public FlowDeskDbContext(DbContextOptions<FlowDeskDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
