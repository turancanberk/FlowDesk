namespace FlowDesk.Domain.Tenancy;

/// <summary>
/// A user's role inside one workspace.
/// </summary>
/// <remarks>
/// The role belongs to the membership, not to the account: the same person can
/// be an Owner in one workspace and a Viewer in another (ADR-0003).
///
/// Values are ordered by authority, which lets "at least this role" checks be
/// written as a comparison instead of a set membership test.
/// </remarks>
public enum MembershipRole
{
    /// <summary>Read-only access.</summary>
    Viewer = 0,

    /// <summary>Creates and updates customers, tickets and tasks.</summary>
    Agent = 1,

    /// <summary>Manages the team and workspace settings.</summary>
    Admin = 2,

    /// <summary>Full authority, including transferring ownership and deleting the workspace.</summary>
    Owner = 3,
}
