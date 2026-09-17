using FlowDesk.Domain.Activity;

namespace FlowDesk.Application.Activity;

/// <param name="ActorDisplayName">
/// Null when the system did it, or when the account is gone. The client shows
/// "Sistem" or "Bilinmeyen kullanıcı" accordingly — which is honest, where
/// inventing a name would not be.
/// </param>
/// <param name="Payload">
/// Raw JSON, passed through. The application does not interpret it: what a line
/// of history reads like is a presentation decision.
/// </param>
public sealed record ActivityItem(
    Guid Id,
    Guid? ActorUserId,
    string? ActorDisplayName,
    ActivityType Type,
    ActivitySubject SubjectType,
    Guid SubjectId,
    string Payload,
    DateTimeOffset OccurredAt);
