using FlowDesk.Domain.Activity;

namespace FlowDesk.Application.Activity.ListActivity;

/// <param name="SubjectType">
/// Set together with <paramref name="SubjectId"/> to read one record's own
/// history rather than the whole workspace's.
/// </param>
public sealed record ListActivityQuery(
    ActivitySubject? SubjectType,
    Guid? SubjectId,
    ActivityType? Type,
    Guid? ActorUserId,
    int? Page,
    int? PageSize);
