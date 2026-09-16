using System.Text.Json;
using FlowDesk.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowDesk.Api.Endpoints;

/// <summary>
/// Liveness and readiness probes.
/// </summary>
/// <remarks>
/// The two answer different questions and must not be collapsed into one.
/// Liveness asks "is this process still working?" — a failing liveness probe
/// means restart me. Readiness asks "can this instance serve traffic right
/// now?" — it fails while a required dependency is unreachable, which should
/// take the instance out of rotation without restarting it.
/// </remarks>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Liveness deliberately runs no checks: dependency failures must not
        // cause a restart loop.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        })
        .WithName("HealthLive")
        .ExcludeFromDescription();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration =>
                registration.Tags.Contains(InfrastructureServiceCollectionExtensions.ReadinessTag),
            ResponseWriter = WriteReadinessResponseAsync,
        })
        .WithName("HealthReady")
        .ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>
    /// Writes a small JSON body naming each dependency and its status, so an
    /// operator can tell which dependency failed without reading server logs.
    /// Exception detail is never included.
    /// </summary>
    private static async Task WriteReadinessResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = (long)entry.Value.Duration.TotalMilliseconds,
            }),
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, HealthJsonOptions.Web),
            context.RequestAborted);
    }
}
