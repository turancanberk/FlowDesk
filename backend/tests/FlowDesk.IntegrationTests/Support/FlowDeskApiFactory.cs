using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Boots the real API host in-process, pointed at a caller-supplied PostgreSQL
/// connection string.
/// </summary>
/// <remarks>
/// The application under test is the real one: the same DI registrations, the
/// same middleware pipeline and the same endpoints that run in production. Only
/// the database location is substituted.
/// </remarks>
public sealed class FlowDeskApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FlowDeskApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Postgres:ConnectionString"] = _connectionString,
                ["Postgres:HealthCheckTimeoutSeconds"] = "3",
            });
        });
    }
}
