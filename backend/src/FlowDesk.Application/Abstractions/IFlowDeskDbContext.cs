using FlowDesk.Domain.Authentication;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Persistence surface the application layer works against.
/// </summary>
/// <remarks>
/// This is not a repository and not a unit-of-work wrapper: it exposes EF Core
/// directly, so projections, <c>AsNoTracking</c> and composed queries all stay
/// available (ADR-0004). Its single job is to keep the dependency arrow
/// pointing inwards — the application states what it needs, infrastructure
/// supplies the PostgreSQL-backed implementation (ADR-0021).
/// </remarks>
public interface IFlowDeskDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Tenant> Tenants { get; }

    /// <summary>
    /// Memberships are deliberately <em>not</em> covered by the workspace query
    /// filter. "Which workspaces do I belong to?" has to look across all of
    /// them, and a filter scoped to the current workspace would make that
    /// question unanswerable.
    /// </summary>
    DbSet<Membership> Memberships { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
