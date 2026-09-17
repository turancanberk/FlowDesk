using System.Text.Json;
using FlowDesk.Domain.Activity;

namespace FlowDesk.Api.Contracts;

/// <param name="ActorDisplayName">
/// Null when the system did it, or when the account is gone. The client says so
/// rather than inventing a name.
/// </param>
/// <param name="Payload">
/// Passed through as JSON rather than a string, so the client reads an object
/// instead of parsing a string that happens to contain one.
/// </param>
public sealed record ActivityResponse(
    Guid Id,
    Guid? ActorUserId,
    string? ActorDisplayName,
    ActivityType Type,
    ActivitySubject SubjectType,
    Guid SubjectId,
    JsonElement Payload,
    DateTimeOffset OccurredAt);
