using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Observability;

/// <summary>Where telemetry goes, bound from <c>Observability</c>.</summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// OTLP endpoint for metrics, or empty for none.
    /// </summary>
    /// <remarks>
    /// Empty is a supported configuration, as it is for the cache (ADR-0037):
    /// the observability stack sits behind a Compose profile and daily
    /// development does not run it. Metrics are still produced — a test can
    /// listen to the meters — they are simply not exported.
    /// </remarks>
    public string MetricsOtlpEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// How often metrics are pushed, in seconds.
    /// </summary>
    /// <remarks>
    /// Push rather than scrape, because the worker serves no HTTP port and
    /// OpenTelemetry's Prometheus exporter is still beta (ADR-0042).
    /// </remarks>
    [Range(1, 3600)]
    public int MetricsExportIntervalSeconds { get; set; } = 15;
}
