using FlowDesk.Infrastructure.HealthChecks;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FlowDesk.Infrastructure;

/// <summary>
/// Composition entry point for the infrastructure layer. Hosts (API, Worker)
/// call this instead of wiring individual infrastructure services themselves,
/// which keeps concrete technology choices from leaking into the hosts.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Name of the PostgreSQL readiness health check.</summary>
    public const string PostgresHealthCheckName = "postgres";

    /// <summary>Tag applied to checks that must pass before traffic is accepted.</summary>
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddFlowDeskInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddFlowDeskPersistence(configuration);
        services.AddFlowDeskInfrastructureHealthChecks();

        return services;
    }

    private static IServiceCollection AddFlowDeskPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first request: a missing
            // connection string is a deployment error, not a runtime condition.
            .ValidateOnStart();

        // A single pooled data source per application. Registering the data
        // source rather than a raw connection string means callers never build
        // their own connections and pooling stays centralised.
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PostgresOptions>>().Value;

            var builder = new NpgsqlDataSourceBuilder(options.ConnectionString);
            builder.UseLoggerFactory(provider.GetRequiredService<ILoggerFactory>());

            return builder.Build();
        });

        return services;
    }

    private static IServiceCollection AddFlowDeskInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>(
                PostgresHealthCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadinessTag]);

        return services;
    }
}
