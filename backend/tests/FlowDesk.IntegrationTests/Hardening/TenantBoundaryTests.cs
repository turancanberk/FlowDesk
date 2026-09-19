using System.Net;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Hardening;

/// <summary>
/// The tenant boundary, checked against every workspace-scoped endpoint at once.
/// </summary>
/// <remarks>
/// Each module proves its own isolation for the endpoints it thought of. These
/// tests draw from the endpoint catalog instead, so the boundary is checked for
/// every route that exists — including the ones added after the module's tests
/// were written.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TenantBoundaryTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly AzuriteContainerFixture _storage;

    public TenantBoundaryTests(PostgresContainerFixture postgres, AzuriteContainerFixture storage)
    {
        _postgres = postgres;
        _storage = storage;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private FlowDeskApiFactory CreateFactory() =>
        new(_postgres.ConnectionString, storageConnectionString: _storage.ConnectionString);

    /// <summary>
    /// Someone signed in, with a workspace of their own, reaches nothing in a
    /// workspace they do not belong to — and learns nothing either: every
    /// answer is the same 404 a missing workspace gets (ADR-0007).
    /// </summary>
    [Fact]
    public async Task A_stranger_reaches_no_endpoint_of_another_workspace()
    {
        await using var factory = CreateFactory();

        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        var scene = await EndpointScene.CreateAsync(factory, workspace, Cancellation);

        using var stranger = await TestWorkspace.CreateAsync(factory, Cancellation, "Rakip Şirket");

        var mismatches = await ProbeAsync(
            scene, stranger.Client, workspace.Slug, HttpStatusCode.NotFound, EndpointCatalog.All);

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    [Fact]
    public async Task An_anonymous_caller_reaches_no_endpoint()
    {
        await using var factory = CreateFactory();

        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        var scene = await EndpointScene.CreateAsync(factory, workspace, Cancellation);

        using var anonymous = factory.CreateApiClient();

        var mismatches = await ProbeAsync(
            scene, anonymous, workspace.Slug, HttpStatusCode.Unauthorized, EndpointCatalog.All);

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>
    /// An owner of two workspaces cannot reach one's records through the
    /// other's address.
    /// </summary>
    /// <remarks>
    /// The case the stranger test cannot catch. Here the caller passes the
    /// workspace check legitimately, so the only thing standing between them
    /// and the record is the tenant filter on the query itself. Owning both
    /// makes the test strict: every role check passes, so a 404 can only come
    /// from the boundary.
    /// </remarks>
    [Fact]
    public async Task A_record_is_not_reachable_under_another_workspaces_address()
    {
        await using var factory = CreateFactory();

        using var home = await TestWorkspace.CreateAsync(factory, Cancellation);
        var homeScene = await EndpointScene.CreateAsync(factory, home, Cancellation);

        var elsewhere = await WorkspaceTestClient.CreateOwnedAsync(home.Client, Cancellation);
        var elsewhereCustomer = await CustomerTestClient.CreateAndReadAsync(
            home.Client, elsewhere.Slug, "Başka Alan Müşterisi", Cancellation);

        var elsewhereScene = await EndpointScene.CreateAsync(
            factory, home.Client, elsewhere.Slug, elsewhereCustomer.Id, Cancellation);

        var recordRoutes = EndpointCatalog.All.Where(endpoint => endpoint.NamesARecord).ToList();

        // Records arranged in the other workspace, requested under the home one.
        var mismatches = await ProbeAsync(
            elsewhereScene, home.Client, homeScene.Slug, HttpStatusCode.NotFound, recordRoutes);

        Assert.NotEmpty(recordRoutes);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>
    /// Arranges each case in <paramref name="scene"/>, sends it under
    /// <paramref name="slug"/> and records every answer other than
    /// <paramref name="expected"/>, and every refused write that nonetheless
    /// left an entry in the history of the scene's workspace — the one that
    /// owns the record.
    /// </summary>
    private static async Task<List<string>> ProbeAsync(
        EndpointScene scene,
        HttpClient caller,
        string slug,
        HttpStatusCode expected,
        IEnumerable<EndpointCase> endpoints)
    {
        var mismatches = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var request = await endpoint.Arrange(scene);

            var historyBefore = endpoint.IsWrite
                ? await EndpointProbe.HistoryLengthAsync(scene.OwnerClient, scene.Slug, Cancellation)
                : 0;

            var actual = await EndpointProbe.SendAsync(caller, slug, request, Cancellation);

            if (actual != expected)
            {
                mismatches.Add($"{endpoint}: beklenen {(int)expected}, gelen {(int)actual}");
                continue;
            }

            if (endpoint.IsWrite)
            {
                var historyAfter = await EndpointProbe.HistoryLengthAsync(
                    scene.OwnerClient, scene.Slug, Cancellation);

                if (historyAfter != historyBefore)
                {
                    mismatches.Add($"{endpoint}: reddedildi ama geçmişe {historyAfter - historyBefore} kayıt yazıldı");
                }
            }
        }

        return mismatches;
    }
}
