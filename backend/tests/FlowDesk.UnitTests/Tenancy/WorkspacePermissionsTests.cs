using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.UnitTests.Tenancy;

/// <summary>
/// The permission matrix from docs/SECURITY.md.
/// </summary>
/// <remarks>
/// Asserted exhaustively rather than by sampling. A permission table is exactly
/// the kind of thing where one wrong row goes unnoticed, and the cost of a
/// wrong row is a user doing something they should not be able to.
/// </remarks>
public sealed class WorkspacePermissionsTests
{
    [Theory]
    // Viewer: read only.
    [InlineData(MembershipRole.Viewer, WorkspaceAction.View, true)]
    [InlineData(MembershipRole.Viewer, WorkspaceAction.ViewMembers, true)]
    [InlineData(MembershipRole.Viewer, WorkspaceAction.Update, false)]
    [InlineData(MembershipRole.Viewer, WorkspaceAction.ManageMembers, false)]
    [InlineData(MembershipRole.Viewer, WorkspaceAction.Delete, false)]
    // Agent: works with the data, does not administer the workspace.
    [InlineData(MembershipRole.Agent, WorkspaceAction.View, true)]
    [InlineData(MembershipRole.Agent, WorkspaceAction.ViewMembers, true)]
    [InlineData(MembershipRole.Agent, WorkspaceAction.Update, false)]
    [InlineData(MembershipRole.Agent, WorkspaceAction.ManageMembers, false)]
    [InlineData(MembershipRole.Agent, WorkspaceAction.Delete, false)]
    // Admin: administers the workspace but cannot delete it.
    [InlineData(MembershipRole.Admin, WorkspaceAction.View, true)]
    [InlineData(MembershipRole.Admin, WorkspaceAction.ViewMembers, true)]
    [InlineData(MembershipRole.Admin, WorkspaceAction.Update, true)]
    [InlineData(MembershipRole.Admin, WorkspaceAction.ManageMembers, true)]
    [InlineData(MembershipRole.Admin, WorkspaceAction.Delete, false)]
    // Owner: everything.
    [InlineData(MembershipRole.Owner, WorkspaceAction.View, true)]
    [InlineData(MembershipRole.Owner, WorkspaceAction.ViewMembers, true)]
    [InlineData(MembershipRole.Owner, WorkspaceAction.Update, true)]
    [InlineData(MembershipRole.Owner, WorkspaceAction.ManageMembers, true)]
    [InlineData(MembershipRole.Owner, WorkspaceAction.Delete, true)]
    public void The_matrix_matches_the_documented_rules(
        MembershipRole role,
        WorkspaceAction action,
        bool expected)
    {
        Assert.Equal(expected, WorkspacePermissions.IsGranted(role, action));
    }

    /// <summary>
    /// An action with no entry must be denied. A permission table that defaults
    /// to "allowed" turns every omission into a hole.
    /// </summary>
    [Fact]
    public void An_unmapped_action_is_denied_for_every_role()
    {
        const WorkspaceAction unmapped = (WorkspaceAction)999;

        Assert.Null(WorkspacePermissions.MinimumRoleFor(unmapped));

        foreach (var role in Enum.GetValues<MembershipRole>())
        {
            Assert.False(WorkspacePermissions.IsGranted(role, unmapped));
        }
    }

    /// <summary>
    /// Every action defined in the enum must appear in the matrix, so adding an
    /// action without deciding who may perform it fails here rather than
    /// failing open at runtime.
    /// </summary>
    [Fact]
    public void Every_defined_action_is_mapped()
    {
        var mapped = WorkspacePermissions.AllActions;

        foreach (var action in Enum.GetValues<WorkspaceAction>())
        {
            Assert.Contains(action, mapped);
        }
    }
}
