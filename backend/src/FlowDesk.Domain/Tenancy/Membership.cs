using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Tenancy;

/// <summary>
/// Links a user to a workspace and carries their role there.
/// </summary>
/// <remarks>
/// This is the record every authorisation decision starts from. No membership
/// means no access, and the role on it is the only thing that grants more than
/// reading (docs/SECURITY.md).
/// </remarks>
public sealed class Membership : ITenantOwned
{
    private Membership()
    {
    }

    private Membership(Guid id, Guid userId, Guid tenantId, MembershipRole role, DateTimeOffset joinedAt)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public MembershipRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>
    /// Creates the membership for the person who created the workspace.
    /// </summary>
    /// <remarks>
    /// A workspace with no Owner would be unmanageable: nobody could invite,
    /// change settings or delete it. Making creation produce an Owner is how
    /// that invariant starts out true.
    /// </remarks>
    public static Membership CreateOwner(Guid userId, Guid tenantId, DateTimeOffset joinedAt) =>
        Create(userId, tenantId, MembershipRole.Owner, joinedAt);

    public static Membership Create(
        Guid userId,
        Guid tenantId,
        MembershipRole role,
        DateTimeOffset joinedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A membership must belong to a user.");
        }

        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A membership must belong to a workspace.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new DomainRuleViolationException($"'{role}' is not a valid membership role.");
        }

        return new Membership(Guid.CreateVersion7(), userId, tenantId, role, joinedAt);
    }

    /// <summary>
    /// Changes this member's role.
    /// </summary>
    /// <param name="role">The new role.</param>
    /// <param name="isLastOwner">
    /// Whether this membership is the workspace's only remaining Owner. The
    /// entity cannot count its siblings, so the caller supplies the answer; the
    /// rule itself is enforced here rather than at each call site.
    /// </param>
    public void ChangeRole(MembershipRole role, bool isLastOwner)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainRuleViolationException($"'{role}' is not a valid membership role.");
        }

        if (isLastOwner && Role is MembershipRole.Owner && role is not MembershipRole.Owner)
        {
            throw new DomainRuleViolationException(
                "The last owner of a workspace cannot give up ownership. Promote another owner first.");
        }

        Role = role;
    }

    /// <summary>True when the role is at least as authoritative as the one required.</summary>
    public bool HasAtLeast(MembershipRole required) => Role >= required;
}
