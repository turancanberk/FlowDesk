using FlowDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Starts a real PostgreSQL instance and applies the migrations once for the
/// whole test run.
/// </summary>
/// <remarks>
/// A real database is used rather than an in-memory or SQLite substitute
/// because the behaviour under test — constraints, concurrency tokens, row
/// locking, query filters — differs materially between providers. A test that
/// passes against a substitute would not tell us the production path works.
///
/// The migrations are applied here rather than by the application under test,
/// so that the same schema the production start-up path would produce is
/// verified once and every test starts from it.
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("flowdesk_test")
        .WithUsername("flowdesk")
        .WithPassword("flowdesk_test_password")
        .WithCleanUp(true)
        .Build();

    /// <summary>Connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new FlowDeskDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Creates a separate, migrated database in the same server and returns its
    /// connection string.
    /// </summary>
    /// <remarks>
    /// For tests that run a background worker. The worker drains every pending
    /// outbox row it can see, and in the shared database that includes the
    /// messages every earlier test left behind — hundreds of them, for tickets
    /// and people that test knows nothing about.
    /// </remarks>
    public async Task<string> CreateIsolatedDatabaseAsync(CancellationToken cancellationToken)
    {
        var name = $"flowdesk_{Guid.CreateVersion7():N}";

        await using (var admin = new Npgsql.NpgsqlConnection(ConnectionString))
        {
            await admin.OpenAsync(cancellationToken);

            // The name is generated above, never input.
#pragma warning disable CA2100
            await using var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
#pragma warning restore CA2100
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionString = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = name,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new FlowDeskDbContext(options);
        await dbContext.Database.MigrateAsync(cancellationToken);

        return connectionString;
    }

    /// <summary>Opens a context against the test database for arranging or asserting state.</summary>
    public FlowDeskDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new FlowDeskDbContext(options);
    }
}
