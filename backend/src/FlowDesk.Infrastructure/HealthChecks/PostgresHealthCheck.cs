using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using FlowDesk.Infrastructure.Persistence;

namespace FlowDesk.Infrastructure.HealthChecks;

/// <summary>
/// Readiness probe for PostgreSQL. Opens a pooled connection and issues a
/// trivial round trip, which verifies that the database is reachable, accepting
/// connections and able to answer a query.
/// </summary>
/// <remarks>
/// A third-party health-check package was deliberately not taken for this: the
/// only maintained one targets an older framework line, and the check itself is
/// a handful of lines that we would rather own outright.
/// </remarks>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public PostgresHealthCheck(
        NpgsqlDataSource dataSource,
        IOptions<PostgresOptions> options,
        ILogger<PostgresHealthCheck> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(options.Value.HealthCheckTimeoutSeconds);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // The probe must not outlive its own budget even if the caller passes
        // a longer-lived token, otherwise an unreachable database turns a
        // readiness probe into a hanging request.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(timeoutSource.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync(timeoutSource.Token);

            stopwatch.Stop();

            return HealthCheckResult.Healthy(
                "PostgreSQL erişilebilir.",
                new Dictionary<string, object> { ["durationMs"] = stopwatch.ElapsedMilliseconds });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "PostgreSQL readiness probe timed out after {TimeoutSeconds}s.",
                _timeout.TotalSeconds);

            return HealthCheckResult.Unhealthy(
                $"PostgreSQL {_timeout.TotalSeconds:0} saniye içinde yanıt vermedi.");
        }
        catch (NpgsqlException exception)
        {
            stopwatch.Stop();
            _logger.LogWarning(exception, "PostgreSQL readiness probe failed.");

            // The message is surfaced to an operator through the probe endpoint,
            // so it states the fact without echoing connection details.
            return HealthCheckResult.Unhealthy("PostgreSQL bağlantısı kurulamadı.");
        }
    }
}
