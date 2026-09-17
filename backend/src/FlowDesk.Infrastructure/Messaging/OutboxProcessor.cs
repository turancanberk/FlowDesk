using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Runs the outbox drain on a schedule for as long as the host is up.
/// </summary>
/// <remarks>
/// Owns only the timing. What a pass does lives in <see cref="OutboxDrain"/>.
///
/// Runs in the worker, not the API. Both hosts draining the same table would
/// work — <c>SKIP LOCKED</c> keeps them off each other's rows — but it would
/// put broker latency inside request-handling processes for no gain.
/// </remarks>
public sealed partial class OutboxProcessor : BackgroundService
{
    private readonly OutboxDrain _drain;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        OutboxDrain drain,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _drain = drain;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger, _options.PollIntervalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            int published;

            try
            {
                published = await _drain.DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The loop must survive anything a single pass
            // throws — a dropped database connection, a broker outage. Letting
            // it escape would end the BackgroundService and stop the outbox
            // draining at all, silently.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogPassFailed(_logger, exception);
                published = 0;
            }

            /*
              Only waits when there was nothing to do. A full batch almost
              certainly means more is waiting, and sleeping through it would
              turn a burst into a queue that drains at one batch per interval.
            */
            if (published < _options.BatchSize)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Outbox işleyici başladı: {PollIntervalSeconds} saniyede bir, {BatchSize} kayıtlık gruplar hâlinde.")]
    private static partial void LogStarted(ILogger logger, int pollIntervalSeconds, int batchSize);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Outbox turu başarısız oldu; bir sonraki turda tekrar denenecek.")]
    private static partial void LogPassFailed(ILogger logger, Exception exception);
}
