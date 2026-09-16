using FlowDesk.Application.Abstractions;

namespace FlowDesk.Infrastructure.Time;

/// <summary>Reads the machine clock. Times are always UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
