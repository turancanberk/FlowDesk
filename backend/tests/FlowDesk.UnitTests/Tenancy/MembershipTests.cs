using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.UnitTests.Tenancy;

public sealed class MembershipTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void The_creator_of_a_workspace_becomes_its_owner()
    {
        var membership = Membership.CreateOwner(UserId, TenantId, Now);

        Assert.Equal(MembershipRole.Owner, membership.Role);
        Assert.Equal(UserId, membership.UserId);
        Assert.Equal(TenantId, membership.TenantId);
    }

    /// <summary>
    /// A workspace with no owner could not be managed by anyone: nobody could
    /// invite, change settings or delete it.
    /// </summary>
    [Theory]
    [InlineData(MembershipRole.Admin)]
    [InlineData(MembershipRole.Agent)]
    [InlineData(MembershipRole.Viewer)]
    public void The_last_owner_cannot_give_up_ownership(MembershipRole newRole)
    {
        var membership = Membership.CreateOwner(UserId, TenantId, Now);

        Assert.Throws<DomainRuleViolationException>(
            () => membership.ChangeRole(newRole, isLastOwner: true));
        Assert.Equal(MembershipRole.Owner, membership.Role);
    }

    [Fact]
    public void An_owner_can_step_down_when_another_owner_remains()
    {
        var membership = Membership.CreateOwner(UserId, TenantId, Now);

        membership.ChangeRole(MembershipRole.Admin, isLastOwner: false);

        Assert.Equal(MembershipRole.Admin, membership.Role);
    }

    [Fact]
    public void The_last_owner_may_still_be_reassigned_the_owner_role()
    {
        var membership = Membership.CreateOwner(UserId, TenantId, Now);

        membership.ChangeRole(MembershipRole.Owner, isLastOwner: true);

        Assert.Equal(MembershipRole.Owner, membership.Role);
    }

    [Fact]
    public void A_membership_must_belong_to_a_user_and_a_workspace()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => Membership.Create(Guid.Empty, TenantId, MembershipRole.Agent, Now));

        Assert.Throws<DomainRuleViolationException>(
            () => Membership.Create(UserId, Guid.Empty, MembershipRole.Agent, Now));
    }

    [Fact]
    public void An_undefined_role_is_rejected()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => Membership.Create(UserId, TenantId, (MembershipRole)99, Now));
    }

    /// <summary>
    /// Roles are ordered by authority so that "at least this role" reads as a
    /// comparison rather than a set membership test.
    /// </summary>
    [Theory]
    [InlineData(MembershipRole.Owner, MembershipRole.Viewer, true)]
    [InlineData(MembershipRole.Admin, MembershipRole.Agent, true)]
    [InlineData(MembershipRole.Agent, MembershipRole.Agent, true)]
    [InlineData(MembershipRole.Viewer, MembershipRole.Agent, false)]
    [InlineData(MembershipRole.Agent, MembershipRole.Admin, false)]
    public void Role_authority_is_ordered(MembershipRole role, MembershipRole required, bool expected)
    {
        var membership = Membership.Create(UserId, TenantId, role, Now);

        Assert.Equal(expected, membership.HasAtLeast(required));
    }
}
