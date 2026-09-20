using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace FlowDesk.Infrastructure.Observability;

/// <summary>
/// Logging and metrics, set up the same way in both hosts.
/// </summary>
/// <remarks>
/// One call for both, because the point of this phase is that a single
/// request can be followed from the API into the worker: that only works if
/// both write the same shape of log and both continue the same trace
/// (ADR-0042).
/// </remarks>
public static class ObservabilityServiceCollectionExtensions
{
    /// <param name="serviceName">
    /// Names the host in every log line and on every metric, so two hosts
    /// writing to one place stay tellable apart.
    /// </param>
    /// <param name="humanReadableLogs">
    /// True in development, where a person reads the console. Elsewhere the
    /// console is read by a machine and the logs are JSON.
    /// </param>
    public static IServiceCollection AddFlowDeskObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool humanReadableLogs)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddLogging(services, configuration, serviceName, humanReadableLogs);
        AddMetricsAndTraces(services, configuration, serviceName);

        return services;
    }

    private static void AddLogging(
        IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool humanReadableLogs)
    {
        /*
          The host's own console provider goes first. Left in place it would
          write every event a second time, in a different shape — the one thing
          structured logging is meant to end.
        */
        services.AddLogging(builder => builder.ClearProviders());

        /*
          preserveStaticLogger: the container's logger is this host's own and
          Serilog's global Log.Logger is left alone. Two hosts in one process —
          which is exactly how the end-to-end tests run the API and the worker —
          would otherwise fight over it, and whichever started last would take
          the other's log output with it (found in Phase 18).
        */
        services.AddSerilog(
            (provider, logger) =>
        {
            logger
                .MinimumLevel.Information()
                /*
                  The framework's own chatter is not news. What is left at
                  Information is the application's: a request finished, a
                  message was published, a consumer handled one.
                */
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                /*
                  Npgsql logs every command it runs, with the SQL text, at
                  Information. That is one line per query in a log meant to be
                  read, and it puts query text where nobody asked for it.
                  Failures still come through at Warning.
                */
                .MinimumLevel.Override("Npgsql", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                /*
                  Reads sinks registered in the container. Nothing in the
                  product uses this; the log hygiene tests do, which is how
                  they read what the running application actually wrote
                  (docs/SECURITY.md §12).
                */
                .ReadFrom.Services(provider)
                // Anything set under "Serilog" in configuration wins, so a
                // deployment can raise a level without a rebuild.
                .ReadFrom.Configuration(configuration);

            if (humanReadableLogs)
            {
                logger.WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Service} {Message:lj} <{TraceId}>{NewLine}{Exception}",
                    // Logs are read by operators and machines, never formatted
                    // for the developer's regional settings.
                    formatProvider: CultureInfo.InvariantCulture);
            }
            else
            {
                // One JSON object per line, with @tr and @sp carrying the
                // trace and span ids.
                logger.WriteTo.Console(new CompactJsonFormatter());
            }
        },
            preserveStaticLogger: true);
    }

    private static void AddMetricsAndTraces(
        IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var options = configuration.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            /*
              Tracing is configured even with nowhere to send traces. The spans
              are what carry a request's identity into the worker and onto
              every log line it writes there; a trace backend (Tempo, Jaeger)
              can be added later without touching this code (ADR-0042).
            */
            .WithTracing(tracing => tracing.AddSource(FlowDeskTelemetry.ActivitySourceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(FlowDeskTelemetry.MeterName)
                    // Process health: GC, threads, exceptions. Built into the
                    // runtime, so no instrumentation package for it.
                    .AddMeter("System.Runtime");

                if (options.MetricsOtlpEndpoint.Length == 0)
                {
                    return;
                }

                metrics.AddOtlpExporter((exporter, reader) =>
                {
                    exporter.Endpoint = new Uri(options.MetricsOtlpEndpoint);
                    // Prometheus accepts OTLP over HTTP with protobuf bodies.
                    exporter.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;

                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                        options.MetricsExportIntervalSeconds * 1000;
                });
            });
    }
}
