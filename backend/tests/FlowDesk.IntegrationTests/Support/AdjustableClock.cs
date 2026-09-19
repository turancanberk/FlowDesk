using FlowDesk.Application.Abstractions;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// The real time, shifted by an offset the test controls.
/// </summary>
/// <remarks>
/// Shifted rather than frozen, because only the application reads this clock.
/// The JWT handler validates lifetimes against the system time, so a test that
/// wants an expired access token issues it in the past and then lets the
/// application return to the present — the same thing that happens to a token
/// left in a browser tab.
/// </remarks>
internal sealed class AdjustableClock : IClock
{
    public TimeSpan Offset { get; set; }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow + Offset;
}
