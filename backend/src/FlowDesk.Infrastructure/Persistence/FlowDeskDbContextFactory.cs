using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowDesk.Infrastructure.Persistence;

/// <summary>
/// Builds a context for the <c>dotnet ef</c> tools.
/// </summary>
/// <remarks>
/// Design-time tooling cannot start the real host: that would validate options,
/// open a data source and run startup checks, none of which a migration
/// scaffold needs. This factory gives the tools exactly the provider
/// information required to generate SQL.
///
/// The connection string is read from the environment. The fallback points at
/// the local Docker Compose instance and is not a secret; it exists so that
/// <c>dotnet ef migrations add</c> works without extra setup.
/// </remarks>
public sealed class FlowDeskDbContextFactory : IDesignTimeDbContextFactory<FlowDeskDbContext>
{
    private const string EnvironmentVariableName = "Postgres__ConnectionString";

    private const string LocalDevelopmentFallback =
        "Host=localhost;Port=5433;Database=flowdesk;Username=flowdesk;Password=degistir_beni_local_dev";

    public FlowDeskDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(EnvironmentVariableName) ?? LocalDevelopmentFallback;

        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(FlowDeskDbContext).Assembly.FullName))
            .Options;

        return new FlowDeskDbContext(options);
    }
}
