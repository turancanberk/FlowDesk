namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Shares one PostgreSQL, one RabbitMQ and one Azurite container across every
/// integration test class.
/// </summary>
/// <remarks>
/// Starting a container per class would make the suite far slower without
/// improving isolation, which is handled per test instead.
///
/// All three are real services rather than substitutes. The behaviour these
/// tests exist to check — constraints, row locking, query filters, topic
/// routing, acknowledgement, object keys — belongs to the services themselves,
/// and a substitute would agree with whatever the code did.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class IntegrationTestSuite
    : ICollectionFixture<PostgresContainerFixture>,
      ICollectionFixture<RabbitMqContainerFixture>,
      ICollectionFixture<AzuriteContainerFixture>
{
    public const string Name = "FlowDesk integration tests";
}
