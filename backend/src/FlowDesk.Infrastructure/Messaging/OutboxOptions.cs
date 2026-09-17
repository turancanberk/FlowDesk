using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>How the outbox processor paces itself.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// How long to wait after finding nothing to do.
    /// </summary>
    /// <remarks>
    /// Polling rather than listening. PostgreSQL can push notifications, but
    /// that would tie the processor to a live connection and still need a poll
    /// as a fallback for anything written while it was disconnected. A few
    /// seconds of latency on a background message is not worth a second
    /// mechanism.
    /// </remarks>
    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// How many rows one pass claims.
    /// </summary>
    /// <remarks>
    /// Bounded because the claim holds a row lock for the whole batch. A large
    /// batch means a long transaction and a long window in which a crash
    /// re-delivers everything in it.
    /// </remarks>
    [Range(1, 500)]
    public int BatchSize { get; set; } = 20;

    /// <summary>Base delay before a failed message is tried again.</summary>
    [Range(1, 3600)]
    public int RetryBaseSeconds { get; set; } = 10;

    /// <summary>
    /// Ceiling for the back-off.
    /// </summary>
    /// <remarks>
    /// Without one, exponential back-off on a message that keeps failing
    /// eventually schedules it past any horizon anyone will wait for, and the
    /// row looks abandoned rather than retrying.
    /// </remarks>
    [Range(1, 86_400)]
    public int RetryMaximumSeconds { get; set; } = 900;
}
