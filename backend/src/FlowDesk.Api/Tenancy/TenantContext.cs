using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Api.Tenancy;

/// <summary>
/// Holds the workspace resolved for the current request.
/// </summary>
/// <remarks>
/// Mutable and scoped to one request. It is filled in exactly once, by
/// <see cref="WorkspaceResolutionFilter"/>, after the caller's membership has
/// been verified against the database. Nothing else may write to it, which is
/// what lets every downstream reader treat a resolved context as proof of
/// membership.
/// </remarks>
public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    private string _slug = string.Empty;
    private Guid _userId;
    private MembershipRole _role;

    public bool IsResolved { get; private set; }

    public Guid TenantId => IsResolved ? _tenantId : throw NotResolved();

    public string Slug => IsResolved ? _slug : throw NotResolved();

    public Guid UserId => IsResolved ? _userId : throw NotResolved();

    public MembershipRole Role => IsResolved ? _role : throw NotResolved();

    internal void Resolve(Guid tenantId, string slug, Guid userId, MembershipRole role)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException(
                "The workspace for this request has already been resolved and cannot be changed.");
        }

        _tenantId = tenantId;
        _slug = slug;
        _userId = userId;
        _role = role;
        IsResolved = true;
    }

    private static InvalidOperationException NotResolved() =>
        new("No workspace is in scope for this request. Endpoints under /api/workspaces/{workspaceSlug} must apply the workspace resolution filter.");
}
