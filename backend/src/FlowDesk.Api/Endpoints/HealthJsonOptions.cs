using System.Text.Json;

namespace FlowDesk.Api.Endpoints;

/// <summary>
/// Serializer options for probe responses, built once rather than per request.
/// </summary>
internal static class HealthJsonOptions
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };
}
