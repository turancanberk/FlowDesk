using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace FlowDesk.Api.Common;

/// <summary>
/// What the API adds to the shared observability set-up: the request itself.
/// </summary>
public static class ApiObservabilityExtensions
{
    /// <summary>Names the API in logs and metrics.</summary>
    public const string ServiceName = "flowdesk-api";

    public static WebApplicationBuilder AddFlowDeskApiObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddFlowDeskObservability(
            builder.Configuration, ServiceName, builder.Environment.IsDevelopment());

        builder.Services
            .AddOpenTelemetry()
            /*
              Every request becomes a span. That span is the trace an outbox row
              carries into the worker, which is what lets a notice sent minutes
              later be traced back to the click that caused it (ADR-0042).
            */
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
            // Request duration, count and status come from the framework's own
            // meter; nothing in the product has to measure them.
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

        return builder;
    }

    /// <summary>
    /// One log line per request, instead of the framework's several.
    /// </summary>
    /// <remarks>
    /// The line carries who made the request and which workspace it was in —
    /// the two things every support question starts with. It carries no
    /// address, no token and no query string (docs/SECURITY.md §12).
    /// </remarks>
    public static WebApplication UseFlowDeskRequestLogging(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSerilogRequestLogging(options =>
        {
            /*
              This host's logger, not Serilog's global one. The middleware
              defaults to the static Log class, which nothing here configures —
              the request lines would go nowhere at all, and in tests two hosts
              would write into each other's pipeline (found in Phase 18).
            */
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();

            options.GetLevel = (httpContext, _, exception) =>
            {
                if (exception is not null || httpContext.Response.StatusCode >= 500)
                {
                    return LogEventLevel.Error;
                }

                /*
                  Readiness probes run every few seconds for ever. At
                  Information they would be most of the log; at Debug they are
                  still there when someone goes looking.
                */
                return httpContext.Request.Path.StartsWithSegments("/health")
                    ? LogEventLevel.Debug
                    : LogEventLevel.Information;
            };

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

                if (userId is not null)
                {
                    diagnosticContext.Set("UserId", userId.Value);
                }

                // Resolved per request by the workspace filter; absent on the
                // routes that are not scoped to one.
                var tenantContext = httpContext.RequestServices.GetService<ITenantContext>();

                if (tenantContext is { IsResolved: true })
                {
                    diagnosticContext.Set("TenantId", tenantContext.TenantId);
                }
            };
        });

        return app;
    }
}
