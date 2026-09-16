using Testcontainers.PostgreSql;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Starts a real PostgreSQL instance for the duration of the test run.
/// </summary>
/// <remarks>
/// A real database is used rather than an in-memory or SQLite substitute
/// because the behaviour under test — constraints, concurrency tokens, row
/// locking, query filters — differs materially between providers. A test that
/// passes against a substitute would not tell us the production path works.
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("flowdesk_test")
        .WithUsername("flowdesk")
        .WithPassword("flowdesk_test_password")
        // Let the host assign a free port; the developer machine already has
        // something on 5432.
        .WithCleanUp(true)
        .Build();

    /// <summary>Connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
