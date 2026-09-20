using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Keeps every log event a host wrote, so a test can read what it actually
/// said.
/// </summary>
/// <remarks>
/// Registered in the container, which the logging set-up reads sinks from.
/// That is deliberate: the alternative — asserting on the code that writes
/// logs — would pass while the running application wrote something else
/// entirely, and what docs/SECURITY.md §12 forbids is what comes out, not
/// what a method intended.
/// </remarks>
internal sealed class CapturedLogs : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public IReadOnlyList<LogEvent> Events => [.. _events];

    /// <summary>
    /// Every event rendered as one string: the message with its properties
    /// filled in, plus any exception.
    /// </summary>
    /// <remarks>
    /// Rendered rather than inspected property by property, because a secret
    /// leaks the same way whether it sits in a property, in the message or in
    /// an exception's text.
    /// </remarks>
    public IReadOnlyList<string> Rendered =>
    [
        .. Events.Select(logEvent =>
        {
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

            logEvent.RenderMessage(writer, System.Globalization.CultureInfo.InvariantCulture);

            foreach (var property in logEvent.Properties)
            {
                writer.Write($" {property.Key}={property.Value}");
            }

            if (logEvent.Exception is not null)
            {
                writer.Write($" {logEvent.Exception}");
            }

            return writer.ToString();
        }),
    ];

    /// <summary>The trace ids the host wrote events under.</summary>
    public IReadOnlyList<string> TraceIds =>
    [
        .. Events
            .Where(logEvent => logEvent.TraceId is not null)
            .Select(logEvent => logEvent.TraceId!.Value.ToString())
            .Distinct(StringComparer.Ordinal),
    ];

    public bool Mentions(string secret) =>
        Rendered.Any(line => line.Contains(secret, StringComparison.OrdinalIgnoreCase));
}
