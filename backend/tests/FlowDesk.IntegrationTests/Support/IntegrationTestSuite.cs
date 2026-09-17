namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Shares one PostgreSQL and one RabbitMQ container across every integration
/// test class.
/// </summary>
/// <remarks>
/// Starting a container per class would make the suite far slower without
/// improving isolation, which is handled per test instead.
///
/// Both are real services rather than substitutes. The behaviour these tests
/// exist to check — constraints, row locking, query filters, topic routing,
/// acknowledgement — belongs to the services themselves, and a substitute would
/// agree with whatever the code did.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class IntegrationTestSuite
    : ICollectionFixture<PostgresContainerFixture>, ICollectionFixture<RabbitMqContainerFixture>
{
    public const string Name = "FlowDesk integration tests";
}
