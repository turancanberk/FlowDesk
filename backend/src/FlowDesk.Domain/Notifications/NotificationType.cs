namespace FlowDesk.Domain.Notifications;

/// <summary>What a notification is about.</summary>
/// <remarks>
/// Deliberately small. A notification for every event would train people to
/// ignore all of them; these are the two that mean "something is now yours".
/// </remarks>
public enum NotificationType
{
    /// <summary>A ticket was assigned to the recipient.</summary>
    TicketAssigned = 0,

    /// <summary>Someone commented on a ticket assigned to the recipient.</summary>
    TicketCommented = 1,
}
