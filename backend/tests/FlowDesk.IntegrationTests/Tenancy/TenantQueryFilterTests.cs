using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.Tenancy;

/// <summary>
/// Verifies that every tenant-owned entity is enrolled in the workspace query
/// filter.
/// </summary>
/// <remarks>
/// This is the second of the three isolation layers (docs/SECURITY.md) and the
/// one most easily forgotten: a new entity is added, the interface is
/// implemented, and nobody notices that a filter was never applied — until one
/// workspace's rows appear in another's list.
///
/// Asserting on the model rather than on query results means the guarantee
/// holds for entities that do not exist yet. The first business entity to rely
/// on it is Customer in Faz 06, which is also where the filter gets its
/// behavioural test against real rows.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class TenantQueryFilterTests
{
    private readonly PostgresContainerFixture _postgres;

    public TenantQueryFilterTests(PostgresContainerFixture postgres) => _postgres = postgres;

    [Fact]
    public void Every_tenant_owned_entity_has_a_workspace_query_filter()
    {
        using var dbContext = _postgres.CreateDbContext();

        var unfiltered = dbContext.Model
            .GetEntityTypes()
            .Where(entityType => typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            .Where(entityType => entityType.ClrType != typeof(Membership))
            .Where(entityType => entityType.GetDeclaredQueryFilters().Count == 0)
            .Select(entityType => entityType.ClrType.Name)
            .ToArray();

        Assert.Empty(unfiltered);
    }

    /// <summary>
    /// Memberships must stay unfiltered. "Which workspaces do I belong to?" has
    /// to look across all of them, and a filter scoped to the current workspace
    /// would make that question unanswerable.
    /// </summary>
    [Fact]
    public void Memberships_are_deliberately_left_unfiltered()
    {
        using var dbContext = _postgres.CreateDbContext();

        var membership = dbContext.Model.FindEntityType(typeof(Membership));

        Assert.NotNull(membership);
        Assert.Empty(membership.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Membership_is_recognised_as_tenant_owned()
    {
        // Guards the exclusion above: if Membership stopped implementing the
        // marker, the exclusion would silently become meaningless.
        Assert.True(typeof(ITenantOwned).IsAssignableFrom(typeof(Membership)));
    }
}
