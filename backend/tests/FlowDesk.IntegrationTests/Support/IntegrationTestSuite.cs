namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Shares a single PostgreSQL container across every integration test class.
/// Starting a container per class would make the suite far slower without
/// improving isolation, which is handled per test instead.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestSuite : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "FlowDesk integration tests";
}
