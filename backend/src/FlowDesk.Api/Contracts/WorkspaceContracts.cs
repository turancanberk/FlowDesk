using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Api.Contracts;

/// <summary>
/// A workspace as returned to the client.
/// </summary>
/// <param name="Role">
/// The caller's own role. The frontend uses it to decide what to show; the
/// backend decides what is allowed regardless of what the frontend shows
/// (docs/SECURITY.md).
/// </param>
public sealed record WorkspaceResponse(
    Guid Id,
    string Name,
    string Slug,
    MembershipRole Role,
    DateTimeOffset CreatedAt);
