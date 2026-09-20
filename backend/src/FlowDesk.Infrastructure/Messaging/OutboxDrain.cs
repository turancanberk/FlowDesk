using System.Diagnostics;

// FlowDesk.Infrastructure.Activity (the audit recorder) shadows the type name
// inside this namespace, so the tracing type is aliased rather than renamed.
using TraceActivity = System.Diagnostics.Activity;
using System.Text;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Observability;
using FlowDesk.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Moves one batch of queued messages from the outbox to the broker.
/// </summary>
/// <remarks>
/// Separate from <see cref="OutboxProcessor"/>, which owns only the schedule.
/// A pass is the thing with rules — how rows are claimed, what a failure does,
/// how long to wait before trying again — and keeping it apart from the loop
/// means it can be reasoned about, and exercised, one pass at a time.
///
/// <para>
/// Each pass claims a bounded batch with <c>FOR UPDATE SKIP LOCKED</c>, so
/// several workers can run at once and take different rows instead of queueing
/// behind one another. Rows another worker holds are skipped rather than waited
/// for, which is the whole point: waiting would serialise the workers and make
/// the second one pointless.
/// </para>
/// </remarks>
public sealed partial class OutboxDrain
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDrain> _logger;

    public OutboxDrain(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDrain> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Claims one batch, publishes it, and returns how many rows it handled.</summary>
    public async Task<int> DrainOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IFlowDeskDbContext>();
        var broker = scope.ServiceProvider.GetRequiredService<IBrokerPublisher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        return await dbContext.ExecuteInTransactionAsync(
            async token =>
            {
                var now = clock.UtcNow;

                /*
                  SKIP LOCKED, so a second worker takes different rows rather
                  than blocking on these. The lock lives until this transaction
                  ends, which is what stops the same message being published
                  twice by two workers in the same moment.

                  Ordered by NextAttemptAt so the oldest ready message goes
                  first; without an order the batch would be whatever the
                  planner happened to reach, and a message could sit behind
                  newer ones indefinitely.
                */
                var claimed = await dbContext.OutboxMessages
                    .FromSql($"""
                        SELECT * FROM "OutboxMessages"
                        WHERE "ProcessedAt" IS NULL AND "NextAttemptAt" <= {now}
                        ORDER BY "NextAttemptAt"
                        LIMIT {_options.BatchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .ToListAsync(token);

                if (claimed.Count == 0)
                {
                    return 0;
                }

                foreach (var message in claimed)
                {
                    await PublishAsync(broker, message, now, token);
                }

                await dbContext.SaveChangesAsync(token);

                /*
                  Counted after the batch, from the same transaction: how many
                  rows are still waiting. A backlog that keeps growing is the
                  first sign that the broker, or a consumer, has stopped
                  keeping up.
                */
                var pending = await dbContext.OutboxMessages
                    .CountAsync(candidate => candidate.ProcessedAt == null, token);

                FlowDeskTelemetry.ReportOutboxBacklog(pending);

                return claimed.Count;
            },
            cancellationToken);
    }

    private async Task PublishAsync(
        IBrokerPublisher broker,
        OutboxMessage message,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        /*
          A span per message, continuing the trace stored with the row. The
          request that queued it finished long ago; this is what makes the
          publish, the delivery and the consumer's work show up as its
          consequences rather than as unrelated background noise (ADR-0042).
        */
        using var activity = StartPublishActivity(message);

        try
        {
            await broker.PublishAsync(
                message.Id,
                message.Type,
                message.RoutingKey,
                message.OccurredAt,
                Encoding.UTF8.GetBytes(message.Payload),
                activity?.Id ?? message.TraceParent,
                cancellationToken);

            message.MarkProcessed(now);

            FlowDeskTelemetry.OutboxPublished.Add(1, new KeyValuePair<string, object?>("type", message.Type));
        }
#pragma warning disable CA1031 // One message's failure must not abandon the rest
        // of the batch: the broker client raises several unrelated exception
        // types, and a single bad row would otherwise roll back work that
        // succeeded.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            var retryAfter = BackOffFor(message.AttemptCount);

            message.MarkFailed(exception.Message, retryAfter, now);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

            FlowDeskTelemetry.OutboxPublishFailures.Add(
                1, new KeyValuePair<string, object?>("type", message.Type));

            LogPublishFailed(_logger, message.Id, message.AttemptCount, retryAfter, exception);
        }
    }

    private static TraceActivity? StartPublishActivity(OutboxMessage message)
    {
        var name = $"outbox publish {message.Type}";

        // Producer, and parented to the stored context when there is one; a
        // message queued outside any trace simply starts its own.
        return ActivityContext.TryParse(message.TraceParent, traceState: null, out var parent)
            ? FlowDeskTelemetry.Source.StartActivity(name, ActivityKind.Producer, parent)
            : FlowDeskTelemetry.Source.StartActivity(name, ActivityKind.Producer);
    }

    /// <summary>
    /// Exponential back-off, capped.
    /// </summary>
    /// <remarks>
    /// Doubling keeps a transient failure from becoming a tight loop against a
    /// broker that is already struggling. The cap keeps a message that keeps
    /// failing from being scheduled past any horizon anyone will wait for — at
    /// which point the row looks abandoned rather than retrying.
    /// </remarks>
    private TimeSpan BackOffFor(int attemptCount)
    {
        // Shift rather than Math.Pow, and bounded, so a long-failing message
        // cannot overflow its way to a negative delay.
        var exponent = Math.Min(attemptCount, 16);
        var seconds = (double)_options.RetryBaseSeconds * (1L << exponent);

        return TimeSpan.FromSeconds(Math.Min(seconds, _options.RetryMaximumSeconds));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Outbox mesajı yayınlanamadı: {MessageId} (deneme {AttemptCount}); {RetryAfter} sonra tekrar denenecek.")]
    private static partial void LogPublishFailed(
        ILogger logger,
        Guid messageId,
        int attemptCount,
        TimeSpan retryAfter,
        Exception exception);
}
