using System.Text.RegularExpressions;
using FlowDesk.IntegrationTests.Support;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.IntegrationTests.Hardening;

/// <summary>
/// The endpoint catalog and the running application describe the same API.
/// </summary>
/// <remarks>
/// This is what makes the role matrix and the tenant boundary tests complete
/// rather than merely long. Without it, a new endpoint would simply be absent
/// from both, and absence fails nothing.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed partial class EndpointCoverageTests
{
    private readonly PostgresContainerFixture _postgres;

    public EndpointCoverageTests(PostgresContainerFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Every_exposed_endpoint_is_either_in_the_catalog_or_excluded_with_a_reason()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        // The endpoint data source is filled while the host starts.
        using var _ = factory.CreateClient();

        var exposed = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(Describe)
            .ToHashSet(StringComparer.Ordinal);

        var accounted = EndpointCatalog.All
            .Select(endpoint => $"{endpoint.Method} {EndpointCatalog.ScopedTemplate(endpoint)}")
            .Concat(EndpointCatalog.OutOfScope.Keys)
            .ToHashSet(StringComparer.Ordinal);

        var unlisted = exposed.Except(accounted).Order(StringComparer.Ordinal).ToList();
        var stale = accounted.Except(exposed).Order(StringComparer.Ordinal).ToList();

        // Named so a failure says which way the two have drifted.
        Assert.True(unlisted.Count == 0, "Katalogda olmayan uç noktalar: " + string.Join(", ", unlisted));
        Assert.True(stale.Count == 0, "Uygulamada olmayan katalog kayıtları: " + string.Join(", ", stale));
    }

    [Fact]
    public void The_catalog_names_each_endpoint_once()
    {
        var duplicates = EndpointCatalog.All
            .GroupBy(endpoint => endpoint.ToString(), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    private static IEnumerable<string> Describe(RouteEndpoint endpoint)
    {
        var template = Normalise(endpoint.RoutePattern.RawText ?? string.Empty);

        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

        // Health checks answer any method, and are listed that way.
        return methods is null or { Count: 0 }
            ? [$"* {template}"]
            : methods.Select(method => $"{method} {template}");
    }

    /// <summary>
    /// Drops route constraints and the trailing slash a group's "/" leaves
    /// behind, so the catalog can be written the way routes are read.
    /// </summary>
    private static string Normalise(string rawText)
    {
        var withoutConstraints = RouteConstraint().Replace(rawText, "{$1}");
        var rooted = withoutConstraints.StartsWith('/') ? withoutConstraints : "/" + withoutConstraints;

        return rooted.Length > 1 ? rooted.TrimEnd('/') : rooted;
    }

    [GeneratedRegex(@"\{([^}:]+):[^}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteConstraint();
}
