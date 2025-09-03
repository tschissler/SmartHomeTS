namespace SharedContracts
{
    public record InfluxRecord
        {
        public string Category { get; init; }
        public string SensorType { get; init; }
        public string Meassurement { get; init; }
        public MeassurementType MeassurementType { get; init; }
        public decimal Value { get; init; }
        public string Unit { get; init; }

    }
}
