namespace FlowDesk.Domain.Activity;

/// <summary>
/// What happened.
/// </summary>
/// <remarks>
/// Deliberately a list of decisions rather than of writes. Every update is not
/// an event: recording each edit to a description would bury the moment a
/// ticket changed hands under noise, and a feed nobody reads records nothing.
///
/// Numbers are explicit, because the stored value is the number. Inserting a
/// member in the middle without a number would rewrite the meaning of every row
/// already written.
/// </remarks>
public enum ActivityType
{
    CustomerCreated = 0,
    CustomerArchived = 1,
    CustomerRestored = 2,

    TicketCreated = 10,
    TicketStatusChanged = 11,
    TicketAssigned = 12,
    TicketUnassigned = 13,
    TicketCommented = 14,
    TicketDeleted = 15,

    TaskCreated = 20,
    TaskCompleted = 21,
    TaskDeleted = 22,

    MemberInvited = 30,
    MemberJoined = 31,
    MemberRoleChanged = 32,
    MemberRemoved = 33,

    AttachmentUploaded = 40,
    AttachmentDeleted = 41,
}

/// <summary>Which kind of record an event is about.</summary>
/// <remarks>
/// Kept separate from <see cref="ActivityType"/> so a record's own history can
/// be read without knowing which events can happen to it — "everything about
/// this customer" is one query, whatever we add later.
/// </remarks>
public enum ActivitySubject
{
    Customer = 0,
    Ticket = 1,
    TaskItem = 2,
    Member = 3,
    Attachment = 4,
}
