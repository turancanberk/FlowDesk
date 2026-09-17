using FlowDesk.Domain.Activity;

namespace FlowDesk.Application.Activity;

/// <summary>
/// Records that something happened, in the caller's own transaction.
/// </summary>
/// <remarks>
/// Written directly rather than published as a message, and that is a decision
/// rather than a shortcut. An audit record has to land with the change it
/// describes: if the change commits and the record does not, the history is
/// wrong in the one way that matters — it says nothing happened. Going through
/// the outbox would make it eventual, and "eventually there will be a record
/// that this was deleted" is not an audit trail (ADR-0036).
///
/// <para>
/// Like <c>IMessagePublisher</c>, this adds a row and does not save. The
/// caller's own <c>SaveChangesAsync</c> is what commits it, which is exactly
/// the guarantee.
/// </para>
/// </remarks>
public interface IActivityRecorder
{
    /// <summary>
    /// Adds an event to the current unit of work.
    /// </summary>
    /// <param name="payload">
    /// Display context. Must contain nothing sensitive: no password, no token,
    /// no invitation link, no message body (docs/SECURITY.md).
    /// </param>
    void Record(
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        object payload);

    /// <summary>
    /// Records an event for a workspace the caller is not yet in.
    /// </summary>
    /// <remarks>
    /// One legitimate case: accepting an invitation. The person is not a member
    /// until that moment, so there is no resolved workspace to read — the
    /// invitation itself says which one, and the event belongs to it.
    ///
    /// <para>
    /// Separate from <see cref="Record"/> on purpose. The ordinary method takes
    /// no workspace, which is what makes it impossible to record an event
    /// against the wrong one; naming a workspace explicitly should look
    /// unusual at the call site, because it is.
    /// </para>
    /// </remarks>
    void RecordOutsideWorkspace(
        Guid tenantId,
        Guid? actorUserId,
        ActivityType type,
        ActivitySubject subjectType,
        Guid subjectId,
        object payload);
}
