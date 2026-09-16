using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Tenancy;

/// <summary>
/// The things a member can be allowed to do inside a workspace.
/// </summary>
/// <remarks>
/// Named after actions rather than roles so that call sites read as
/// "may this caller rename the workspace?" instead of "is this caller an
/// admin?". Changing who may do something then means editing one table instead
/// of hunting for role comparisons scattered across endpoints.
/// </remarks>
public enum WorkspaceAction
{
    View,
    Update,
    Delete,
    ViewMembers,
    ManageMembers,
}

/// <summary>
/// The single source of truth for what each role may do.
/// </summary>
/// <remarks>
/// Kept as one table, in one file, because a permission matrix scattered across
/// endpoints drifts: a rule gets tightened in one place and left alone in
/// another, and the difference stays invisible until someone exploits it.
///
/// The full matrix is documented in docs/SECURITY.md. Entries for customers,
/// tickets and tasks arrive with those modules.
/// </remarks>
public static class WorkspacePermissions
{
    private static readonly Dictionary<WorkspaceAction, MembershipRole> MinimumRoles = new()
    {
        [WorkspaceAction.View] = MembershipRole.Viewer,
        [WorkspaceAction.ViewMembers] = MembershipRole.Viewer,
        [WorkspaceAction.Update] = MembershipRole.Admin,
        [WorkspaceAction.ManageMembers] = MembershipRole.Admin,
        [WorkspaceAction.Delete] = MembershipRole.Owner,
    };

    public static bool IsGranted(MembershipRole role, WorkspaceAction action) =>
        // An unmapped action is denied rather than allowed: a missing entry
        // must fail closed.
        MinimumRoles.TryGetValue(action, out var minimumRole) && role >= minimumRole;

    /// <summary>The least authoritative role that is granted the action.</summary>
    public static MembershipRole? MinimumRoleFor(WorkspaceAction action) =>
        MinimumRoles.TryGetValue(action, out var minimumRole) ? minimumRole : null;

    /// <summary>Every action, for exhaustiveness checks in tests.</summary>
    public static IReadOnlyCollection<WorkspaceAction> AllActions => MinimumRoles.Keys;
}
