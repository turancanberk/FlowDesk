namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// A signed-in owner, their workspace and one customer to work against.
/// </summary>
/// <remarks>
/// Almost every ticket and task test needs these three things before it can
/// assert anything, and building them inline would bury the actual subject of
/// each test under twelve lines of setup.
/// </remarks>
internal sealed record TestWorkspace(SignedInUser User, string Slug, Guid CustomerId)
    : IDisposable
{
    public static async Task<TestWorkspace> CreateAsync(
        FlowDeskApiFactory factory,
        CancellationToken cancellationToken,
        string customerName = "Acme Teknoloji")
    {
        var user = await AuthTestClient.SignInNewUserAsync(factory, cancellationToken);
        var workspace = await WorkspaceTestClient.CreateOwnedAsync(user.Client, cancellationToken);
        var customer = await CustomerTestClient.CreateAndReadAsync(
            user.Client, workspace.Slug, customerName, cancellationToken);

        return new TestWorkspace(user, workspace.Slug, customer.Id);
    }

    /// <summary>The owner's authenticated client.</summary>
    public HttpClient Client => User.Client;

    public void Dispose() => User.Dispose();
}
