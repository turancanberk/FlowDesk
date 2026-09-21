using FlowDesk.Application.Abstractions;

namespace FlowDesk.Infrastructure.Time;

/// <summary>Reads the machine clock. Times are always UTC.</summary>
/// <remarks>
/// Truncated to whole microseconds, which is what PostgreSQL's
/// <c>timestamp with time zone</c> stores. .NET keeps 100-nanosecond ticks, so
/// without this a value is one thing in memory and another after a round trip:
/// the response to a write carries the tick-precision time, and every later
/// read carries the truncated one. Two answers for the same field, differing
/// below a microsecond.
///
/// <para>
/// Found in the final audit (Faz 23), where a test comparing the two failed on
/// CI roughly whenever the tick count was not already a whole microsecond —
/// and passed locally when it happened to be. Truncating here makes the time
/// the application believes in the same time it can store, everywhere, rather
/// than teaching each comparison to tolerate the difference.
/// </para>
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <summary>100-nanosecond ticks in one microsecond.</summary>
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    public DateTimeOffset UtcNow => ToStorageResolution(DateTimeOffset.UtcNow);

    /// <summary>
    /// Drops anything the database cannot keep.
    /// </summary>
    /// <remarks>
    /// Separate and public so the rule can be tested with values chosen for the
    /// purpose. Sampling the machine clock would not do it: on a host whose
    /// clock is already microsecond-resolution — macOS, for one — every reading
    /// is whole, and a test that only samples passes whether this truncates or
    /// not. That is how the difference reached CI unnoticed.
    /// </remarks>
    public static DateTimeOffset ToStorageResolution(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TicksPerMicrosecond), value.Offset);
}
