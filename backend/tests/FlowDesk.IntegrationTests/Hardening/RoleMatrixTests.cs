using System.Net;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Hardening;

/// <summary>
/// Every role against every workspace-scoped endpoint, in one table.
/// </summary>
/// <remarks>
/// The module tests already check the interesting cases one by one. This test
/// checks the rest: that no endpoint was left out, and that a refusal is a
/// refusal — nothing written first and rejected afterwards.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class RoleMatrixTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly AzuriteContainerFixture _storage;

    public RoleMatrixTests(PostgresContainerFixture postgres, AzuriteContainerFixture storage)
    {
        _postgres = postgres;
        _storage = storage;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(MembershipRole.Viewer)]
    [InlineData(MembershipRole.Agent)]
    [InlineData(MembershipRole.Admin)]
    [InlineData(MembershipRole.Owner)]
    public async Task Each_endpoint_admits_exactly_the_roles_the_matrix_allows(MembershipRole role)
    {
        /*
          One host per role. Each registers up to three people, and the
          registration limit is counted per host; sharing one across all four
          roles would run into it.
        */
        await using var factory = new FlowDeskApiFactory(
            _postgres.ConnectionString, storageConnectionString: _storage.ConnectionString);

        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        var scene = await EndpointScene.CreateAsync(factory, workspace, Cancellation);

        using var member = role is MembershipRole.Owner
            ? null
            : await TeamTestClient.AddMemberAsync(factory, workspace.Client, workspace.Slug, role, Cancellation);

        var caller = member?.Client ?? workspace.Client;
        var mismatches = new List<string>();

        foreach (var endpoint in EndpointCatalog.All)
        {
            var request = await endpoint.Arrange(scene);
            var allowed = role >= endpoint.MinimumRole;
            var expected = allowed ? endpoint.Success : HttpStatusCode.Forbidden;

            var historyBefore = allowed || !endpoint.IsWrite
                ? 0
                : await EndpointProbe.HistoryLengthAsync(workspace.Client, workspace.Slug, Cancellation);

            var actual = await EndpointProbe.SendAsync(caller, workspace.Slug, request, Cancellation);

            if (actual != expected)
            {
                mismatches.Add($"{endpoint}: beklenen {(int)expected}, gelen {(int)actual}");
                continue;
            }

            if (!allowed && endpoint.IsWrite)
            {
                var historyAfter = await EndpointProbe.HistoryLengthAsync(
                    workspace.Client, workspace.Slug, Cancellation);

                if (historyAfter != historyBefore)
                {
                    mismatches.Add($"{endpoint}: reddedildi ama geçmişe {historyAfter - historyBefore} kayıt yazıldı");
                }
            }
        }

        Assert.True(mismatches.Count == 0, $"{role}:\n" + string.Join("\n", mismatches));
    }
}
