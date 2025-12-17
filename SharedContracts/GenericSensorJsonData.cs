using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SharedContracts
{
    public record GenericSensorJsonData
    {
        public required string Location { get; init; }
        public required string Device { get; init; }
        public required string SensorType { get; init; }

        [JsonPropertyName("sub_category")]
        public required string SubCategory { get; init; }

        public List<Dictionary<string, decimal>> Temperatures { get; init; } = new();
        public List<Dictionary<string, decimal>> Percentages { get; init; } = new();
    }
}
