using System.Text.Json;
using System.Text.Json.Serialization;

namespace BMWConnector.Models;

public class BmwPayload
{
    [JsonPropertyName("vin")]
    public string Vin { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, BmwDataPoint> Data { get; set; } = new();
}

public class BmwDataPoint
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}
