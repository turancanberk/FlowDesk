using System.Diagnostics.Metrics;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Listens to FlowDesk's own meter and adds up what was recorded.
/// </summary>
/// <remarks>
/// The exporter is not involved: what a test can check is that the
/// application measures what it claims to, and a listener sees exactly the
/// instruments and tags an exporter would (ADR-0042).
/// </remarks>
internal sealed class MeterProbe : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly System.Collections.Concurrent.ConcurrentBag<(string Instrument, string Tags, long Value)>
        _measurements = [];

    public MeterProbe(string meterName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            _measurements.Add((instrument.Name, Describe(tags), measurement)));

        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) =>
            _measurements.Add((instrument.Name, Describe(tags), measurement)));

        _listener.Start();
    }

    /// <summary>Everything recorded for one instrument, optionally filtered by a tag.</summary>
    public long Total(string instrument, string? tagContains = null) =>
        _measurements
            .Where(measurement => measurement.Instrument == instrument)
            .Where(measurement =>
                tagContains is null
                || measurement.Tags.Contains(tagContains, StringComparison.Ordinal))
            .Sum(measurement => measurement.Value);

    /// <summary>Reads the instruments that only report when asked.</summary>
    public void CollectObservable() => _listener.RecordObservableInstruments();

    public void Dispose() => _listener.Dispose();

    private static string Describe(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var parts = new List<string>(tags.Length);

        foreach (var tag in tags)
        {
            parts.Add($"{tag.Key}={tag.Value}");
        }

        return string.Join(",", parts);
    }
}
