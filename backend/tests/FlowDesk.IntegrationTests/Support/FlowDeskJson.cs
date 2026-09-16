using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// JSON settings matching what the API produces and accepts.
/// </summary>
/// <remarks>
/// The API serialises enums by name rather than by number
/// (docs/API_CONVENTIONS.md), so a client that reads responses with default
/// options cannot deserialise a role or a status. Sharing one set of options
/// keeps the test client honest about the contract instead of quietly using a
/// different one.
/// </remarks>
internal static class FlowDeskJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
