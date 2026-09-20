using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FlowDesk.Infrastructure.Observability;

/// <summary>
/// The names and instruments FlowDesk's own telemetry uses.
/// </summary>
/// <remarks>
/// One place, because a meter or source name is a contract: a dashboard, an
/// alert and a test all refer to it by string, and renaming one quietly breaks
/// the others.
///
/// Only what a person would act on is measured. Request rate and duration come
/// from the framework's own meters; what the framework cannot see is the
/// background path — messages waiting in the outbox, deliveries that failed,
/// duplicates the idempotency check caught — and that is what these add.
/// </remarks>
public static class FlowDeskTelemetry
{
    public const string ActivitySourceName = "FlowDesk.Messaging";
    public const string MeterName = "FlowDesk";

    /// <summary>
    /// Spans for the background path: publishing an outbox row and handling a
    /// delivery, both continuing the trace of the request that caused them.
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    /// <summary>Messages handed to the broker.</summary>
    public static readonly Counter<long> OutboxPublished =
        Meter.CreateCounter<long>("flowdesk.outbox.published", unit: "{message}");

    /// <summary>
    /// Failed publish attempts, not failed messages: one message that keeps
    /// failing counts once per attempt, which is what a rising rate means.
    /// </summary>
    public static readonly Counter<long> OutboxPublishFailures =
        Meter.CreateCounter<long>("flowdesk.outbox.publish_failures", unit: "{attempt}");

    /// <summary>
    /// Deliveries a consumer finished, tagged with the consumer and the
    /// outcome: handled, duplicate, unreadable or failed.
    /// </summary>
    public static readonly Counter<long> MessagesConsumed =
        Meter.CreateCounter<long>("flowdesk.messages.consumed", unit: "{message}");

    /// <summary>Cache reads, tagged hit, miss or error (ADR-0037).</summary>
    public static readonly Counter<long> CacheReads =
        Meter.CreateCounter<long>("flowdesk.cache.reads", unit: "{read}");

    /// <summary>
    /// How many messages are waiting to be published, as the last outbox pass
    /// saw it.
    /// </summary>
    /// <remarks>
    /// Reported by the pass rather than queried when the exporter asks: a
    /// gauge that ran a COUNT on every collection would turn the export
    /// interval into database load, and the pass already has the number.
    /// </remarks>
    public static void ReportOutboxBacklog(int pending) => _outboxPending = pending;

    private static int _outboxPending;
    private static ObservableGauge<int>? _outboxPendingGauge;

    /// <summary>
    /// Starts reporting the outbox backlog. Called by the worker only.
    /// </summary>
    /// <remarks>
    /// The API drains nothing, so a gauge there would publish a confident
    /// zero beside the worker's real number — the kind of reading that makes
    /// a dashboard worse than none.
    /// </remarks>
    public static void EnableOutboxBacklogGauge() =>
        _outboxPendingGauge ??= Meter.CreateObservableGauge(
            "flowdesk.outbox.pending",
            () => _outboxPending,
            unit: "{message}",
            description: "Yayınlanmayı bekleyen outbox mesajı sayısı.");

    /// <summary>Outcome tag values, so producers and dashboards agree.</summary>
    public static class Outcomes
    {
        public const string Handled = "handled";
        public const string Duplicate = "duplicate";
        public const string Unreadable = "unreadable";
        public const string Failed = "failed";
        public const string Hit = "hit";
        public const string Miss = "miss";
        public const string Error = "error";
    }
}
