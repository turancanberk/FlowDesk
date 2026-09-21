namespace FlowDesk.Infrastructure.DemoData;

/// <summary>
/// What the seed did, in enough detail to print a useful line and to assert on.
/// </summary>
public sealed record DemoDataSeedResult(
    bool WasSeeded,
    int Accounts,
    int Customers,
    int Tickets,
    int Tasks,
    string? SignInEmail,
    IReadOnlyList<string> ExistingWorkspaces)
{
    public static DemoDataSeedResult Seeded(
        int accounts,
        int customers,
        int tickets,
        int tasks,
        string signInEmail) =>
        new(true, accounts, customers, tickets, tasks, signInEmail, []);

    /// <summary>
    /// The workspaces were already there, so nothing was written.
    /// </summary>
    /// <remarks>
    /// Not an error. Seeding twice is a thing people do by accident, and the
    /// honest answer is "this database already has it" rather than a second
    /// copy of every workspace.
    /// </remarks>
    public static DemoDataSeedResult AlreadyPresent(IReadOnlyList<string> workspaces) =>
        new(false, 0, 0, 0, 0, null, workspaces);
}
