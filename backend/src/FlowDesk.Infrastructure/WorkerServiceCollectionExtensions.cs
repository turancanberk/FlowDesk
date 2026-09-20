using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Observability;
using FlowDesk.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowDesk.Infrastructure;

/// <summary>
/// Everything the Worker host runs, in one call.
/// </summary>
/// <remarks>
/// One call so that the Worker's Program and the tests that start a worker
/// compose it identically. Phase 15 broke the worker with a registration only
/// the API happened to satisfy, and nothing noticed because no test built the
/// worker the way the Worker builds itself.
/// </remarks>
public static class WorkerServiceCollectionExtensions
{
    /// <summary>Names the worker in logs and metrics.</summary>
    public const string ServiceName = "flowdesk-worker";

    /// <param name="humanReadableLogs">
    /// True in development, where a person reads the console (ADR-0042).
    /// </param>
    public static IServiceCollection AddFlowDeskWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        bool humanReadableLogs = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        /*
          Logging and metrics first, so everything that follows — including a
          start-up failure — is written in the shape the rest of the system
          reads.
        */
        services.AddFlowDeskObservability(configuration, ServiceName, humanReadableLogs);

        services.AddFlowDeskInfrastructure(configuration);

        // The consumers, and the host that runs them. Consumers run in the
        // worker and only there (see AddFlowDeskNotificationConsumers).
        services.AddFlowDeskNotificationConsumers();

        /*
          Drains the outbox into the broker. Use cases queue messages by writing
          a row in their own transaction; nothing reaches RabbitMQ until this
          runs (ADR-0033).
        */
        services.AddFlowDeskOutboxProcessor(configuration);

        return services;
    }
}
