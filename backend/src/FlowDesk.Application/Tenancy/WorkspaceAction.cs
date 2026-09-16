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
    ViewInvitations,
    InviteMembers,
    RevokeInvitations,
    ViewCustomers,
    ManageCustomers,
    ArchiveCustomers,
    ViewTickets,
    ManageTickets,
    DeleteTickets,
    CommentOnTickets,
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
        /*
          Pending invitations are visible to anyone in the workspace. They are
          not sensitive on their own — the address was supplied by a teammate —
          and hiding them would leave agents wondering why someone they expect
          has not appeared yet.
        */
        [WorkspaceAction.ViewInvitations] = MembershipRole.Viewer,
        [WorkspaceAction.ViewCustomers] = MembershipRole.Viewer,
        // Agents do the day-to-day work, so creating and editing customers is
        // theirs; taking a record out of circulation is an administrative act.
        [WorkspaceAction.ManageCustomers] = MembershipRole.Agent,
        [WorkspaceAction.ArchiveCustomers] = MembershipRole.Admin,
        [WorkspaceAction.ViewTickets] = MembershipRole.Viewer,
        // Handling requests is the agent's core job: creating, editing,
        // assigning, changing status and commenting.
        [WorkspaceAction.ManageTickets] = MembershipRole.Agent,
        [WorkspaceAction.CommentOnTickets] = MembershipRole.Agent,
        // Deleting destroys a customer conversation, so it stays with admins.
        [WorkspaceAction.DeleteTickets] = MembershipRole.Admin,
        [WorkspaceAction.Update] = MembershipRole.Admin,
        [WorkspaceAction.ManageMembers] = MembershipRole.Admin,
        [WorkspaceAction.InviteMembers] = MembershipRole.Admin,
        [WorkspaceAction.RevokeInvitations] = MembershipRole.Admin,
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
