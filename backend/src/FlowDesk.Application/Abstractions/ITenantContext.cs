using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Abstractions;

/// <summary>
/// The workspace the current request is operating in, and the caller's
/// membership of it.
/// </summary>
/// <remarks>
/// Populated once per request, after the membership has been verified. Reaching
/// this context means the caller has already been proven to belong to the
/// workspace; a use case does not need to re-check that it exists
/// (docs/SECURITY.md).
///
/// Requests outside a workspace — signing in, listing your own workspaces —
/// leave it unresolved, and reading <see cref="TenantId"/> then is a
/// programming error rather than an authorisation failure.
/// </remarks>
public interface ITenantContext
{
    bool IsResolved { get; }

    Guid TenantId { get; }

    string Slug { get; }

    Guid UserId { get; }

    MembershipRole Role { get; }
}
