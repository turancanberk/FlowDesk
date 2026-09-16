using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Dashboard;

/// <summary>
/// The workspace at a glance.
/// </summary>
/// <remarks>
/// One response rather than a box per endpoint. A dashboard that loads through
/// six requests spends its first second rearranging itself, and every one of
/// those requests would repeat the same membership check.
/// </remarks>
/// <param name="GeneratedAt">
/// When the figures were read. Sent so the screen can say how fresh they are
/// rather than implying they are live.
/// </param>
public sealed record DashboardSummary(
    int CustomerCount,
    int OpenTicketCount,
    int UnassignedTicketCount,
    int OverdueTaskCount,
    int DueSoonTaskCount,
    int MemberCount,
    IReadOnlyList<TicketStatusCount> TicketsByStatus,
    IReadOnlyList<RecentTicket> RecentTickets,
    IReadOnlyList<UpcomingTask> UpcomingTasks,
    DateTimeOffset GeneratedAt);

public sealed record TicketStatusCount(TicketStatus Status, int Count);

public sealed record RecentTicket(
    Guid Id,
    int Number,
    string Subject,
    string CustomerName,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset UpdatedAt);

/// <param name="IsOverdue">
/// Computed against the same instant as the counts above, so a task cannot be
/// counted as overdue in one part of the response and not in another.
/// </param>
public sealed record UpcomingTask(
    Guid Id,
    string Title,
    DateTimeOffset DueAt,
    bool IsOverdue,
    string? AssignedUserDisplayName);
