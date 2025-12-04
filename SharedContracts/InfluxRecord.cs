namespace SharedContracts
{
    public abstract record InfluxRecord
        {
        public required string MeasurementId { get; init; }
        public required MeasurementCategory Category { get; init; }
        public required string SensorType { get; init; }
        public required string Location { get; init; }
        public required string Device { get; init; }
        public required string Measurement { get; init; }
        public MeasurementType MeasurementType { get; init; }
    }

    public record InfluxEnergyRecord : InfluxRecord
    {
        public required MeasurementSubCategory SubCategory { get; init; }
        public required decimal Value_Delta_KWh { get; init; } // in kWh
        public required decimal Value_Cumulated_KWh { get; init; } // in kWh

        public InfluxEnergyRecord()
        {
            MeasurementType = MeasurementType.Energy;
        }
    }

    public record InfluxPowerRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required decimal Value_W { get; init; } // in W

        public InfluxPowerRecord()
        {
            MeasurementType = MeasurementType.Power;
        }
    }

    public record InfluxVoltageRecord : InfluxRecord
    {
        public required MeasurementSubCategory SubCategory { get; init; }
        public required decimal Value_V { get; init; } // in V

        public InfluxVoltageRecord()
        {
            MeasurementType = MeasurementType.Voltage;
        }
    }

    public record InfluxPercentageRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required decimal Value_Percent { get; init; } // in Percent

        public InfluxPercentageRecord()
        {
            MeasurementType= MeasurementType.Percent;
        }
    }

    public record InfluxTemperatureRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required decimal Value_DegreeC { get; init; } // in °C

        public InfluxTemperatureRecord()
        {
            MeasurementType = MeasurementType.Temperature;
        }
    }

    public record InfluxStatusRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required decimal Value_Status { get; init; }

        public InfluxStatusRecord()
        {
            MeasurementType = MeasurementType.Status;
        }
    }

    public record InfluxCounterRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required int Value_Counter { get; init; }
        public InfluxCounterRecord()
        {
            MeasurementType = MeasurementType.Counter;
        }
    }
}
