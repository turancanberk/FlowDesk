using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// The JSON settings both the publisher and the consumers use.
/// </summary>
/// <remarks>
/// Shared deliberately. A message written with one set of options and read with
/// another is a bug that only appears once a message is actually in flight, and
/// by then it is in a queue rather than in a test.
///
/// Enums travel as names for the same reason they do over HTTP (ADR-0023):
/// numbers make the wire format depend on declaration order, and a message
/// sitting in a queue across a deployment would change meaning.
/// </remarks>
public static class FlowDeskMessageJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
