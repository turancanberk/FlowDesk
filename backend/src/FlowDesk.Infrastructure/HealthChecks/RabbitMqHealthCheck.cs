using System.Diagnostics;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace FlowDesk.Infrastructure.HealthChecks;

/// <summary>
/// Readiness probe for the message broker.
/// </summary>
/// <remarks>
/// Reports <see cref="HealthStatus.Degraded"/> rather than unhealthy when the
/// broker is unreachable, and that difference is the point. The API serves
/// every read and every write without it; only the messages that would have
/// followed are delayed. Failing readiness would take a working instance out of
/// rotation over a dependency it can work without — and, with every instance
/// failing at once, take the whole product down because e-mail could not be
/// queued.
///
/// The probe still reports it, so an operator sees the broker is down before
/// the backlog does.
/// </remarks>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnection _connection;
    private readonly ILogger<RabbitMqHealthCheck> _logger;

    public RabbitMqHealthCheck(RabbitMqConnection connection, ILogger<RabbitMqHealthCheck> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // The probe must not outlive its own budget even if the caller passes a
        // longer-lived token, otherwise an unreachable broker turns a readiness
        // probe into a hanging request.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(Timeout);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Opening a channel proves more than a reachable socket: it means
            // the connection is authenticated and the broker is accepting work.
            await using var channel = await _connection.OpenChannelAsync(timeoutSource.Token);

            stopwatch.Stop();

            return HealthCheckResult.Healthy(
                "Mesaj kuyruğu erişilebilir.",
                new Dictionary<string, object> { ["durationMs"] = stopwatch.ElapsedMilliseconds });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Mesaj kuyruğu hazırlık kontrolü {TimeoutSeconds} saniyede yanıt alamadı.",
                Timeout.TotalSeconds);

            return HealthCheckResult.Degraded(
                $"Mesaj kuyruğu {Timeout.TotalSeconds:0} saniye içinde yanıt vermedi.");
        }
#pragma warning disable CA1031 // A health probe reports every failure rather than
        // letting one escape and take the endpoint down; the broker client
        // raises several unrelated exception types for an unreachable host.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            stopwatch.Stop();
            _logger.LogWarning(exception, "Mesaj kuyruğu hazırlık kontrolü başarısız.");

            // States the fact without echoing connection details, which reach an
            // operator through the probe endpoint.
            return HealthCheckResult.Degraded("Mesaj kuyruğu bağlantısı kurulamadı.");
        }
    }
}
