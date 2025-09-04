namespace SharedContracts
{
    public record InfluxRecord
        {
        public required DataCategory Category { get; init; }
        public required SubCategory SubCategory { get; init; }
        public required string SensorType { get; init; }
        public required string Measurement { get; init; }
        public MeasurementType MeasurementType { get; init; }
        public decimal Value { get; init; }
        public string? Unit { get; init; }

    }
}
