using FlowDesk.Domain.Authentication;
using FlowDesk.Domain.Activity;
using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Messaging;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
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

    DbSet<Invitation> Invitations { get; }

    /// <summary>
    /// Scoped to the current workspace and filtered to non-archived rows by the
    /// global query filter. Use <c>IgnoreQueryFilters</c> only where an
    /// archived customer is genuinely the subject.
    /// </summary>
    DbSet<Customer> Customers { get; }

    DbSet<Ticket> Tickets { get; }

    DbSet<TicketComment> TicketComments { get; }

    /// <summary>
    /// File records. The bytes live in object storage; this is what makes them
    /// findable, authorisable and removable.
    /// </summary>
    DbSet<Attachment> Attachments { get; }

    /// <summary>Per-workspace ticket numbering counters.</summary>
    DbSet<TenantCounter> TenantCounters { get; }

    DbSet<TaskItem> Tasks { get; }

    /// <summary>
    /// Messages waiting to reach the broker.
    /// </summary>
    /// <remarks>
    /// Not covered by the workspace query filter, and cannot be: the processor
    /// runs in the worker with no workspace context, so a filter would leave it
    /// finding nothing (ADR-0024). The tenant travels inside the payload.
    /// </remarks>
    DbSet<OutboxMessage> OutboxMessages { get; }

    /// <summary>
    /// What each consumer has already handled, so a redelivery acts once.
    /// </summary>
    DbSet<ProcessedMessage> ProcessedMessages { get; }

    /// <summary>
    /// In-app notices, scoped to the current workspace by the query filter.
    /// </summary>
    /// <remarks>
    /// Written by a consumer, which has no workspace context of its own, so the
    /// <c>TenantId</c> comes from the message being handled (ADR-0033).
    /// </remarks>
    DbSet<Notification> Notifications { get; }

    /// <summary>
    /// The workspace's history. Append-only; nothing here updates or removes a
    /// row (ADR-0036).
    /// </summary>
    DbSet<ActivityEvent> ActivityEvents { get; }

    /// <summary>
    /// Runs <paramref name="work"/> inside a database transaction.
    /// </summary>
    /// <remarks>
    /// Needed where two changes only make sense together — taking a ticket
    /// number and inserting the ticket that uses it. The application layer says
    /// it needs atomicity; how a transaction is opened is infrastructure's
    /// business.
    /// </remarks>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken);

    /// <summary>
    /// Locks a workspace's counter row and returns the next ticket number.
    /// </summary>
    /// <remarks>
    /// Row locking is a database capability with no portable LINQ expression,
    /// so it is declared here and implemented by the provider. Must be called
    /// inside <see cref="ExecuteInTransactionAsync"/>: the lock is held until
    /// that transaction ends, and that is what stops two simultaneous
    /// creations from taking the same number.
    /// </remarks>
    Task<int> TakeNextTicketNumberAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
