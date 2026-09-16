using FlowDesk.Domain.Tickets;

namespace FlowDesk.Api.Contracts;

/// <param name="GeneratedAt">
/// When the figures were read, so the screen can say how fresh they are rather
/// than implying they are live.
/// </param>
public sealed record DashboardResponse(
    int CustomerCount,
    int OpenTicketCount,
    int UnassignedTicketCount,
    int OverdueTaskCount,
    int DueSoonTaskCount,
    int MemberCount,
    IReadOnlyList<TicketStatusCountResponse> TicketsByStatus,
    IReadOnlyList<RecentTicketResponse> RecentTickets,
    IReadOnlyList<UpcomingTaskResponse> UpcomingTasks,
    DateTimeOffset GeneratedAt);

public sealed record TicketStatusCountResponse(TicketStatus Status, int Count);

public sealed record RecentTicketResponse(
    Guid Id,
    int Number,
    string Subject,
    string CustomerName,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset UpdatedAt);

public sealed record UpcomingTaskResponse(
    Guid Id,
    string Title,
    DateTimeOffset DueAt,
    bool IsOverdue,
    string? AssignedUserDisplayName);
