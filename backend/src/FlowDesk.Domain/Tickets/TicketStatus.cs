namespace FlowDesk.Domain.Tickets;

/// <summary>Where a support request stands.</summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,

    /// <summary>Blocked on someone outside the team, usually the customer.</summary>
    Waiting = 2,

    /// <summary>Handled, but not yet confirmed and filed away.</summary>
    Resolved = 3,
    Closed = 4,
}

/// <summary>How urgently a request needs attention.</summary>
public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3,
}
