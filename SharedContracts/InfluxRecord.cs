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
    
    public record InfluxVolumeRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required decimal Value_Volume { get; init; }
        public InfluxVolumeRecord()
        {
            MeasurementType = MeasurementType.Volume;
        }
    }

    /// <summary>
    /// A distance in km — range and odometer of a vehicle. Its own table for the same reason
    /// every other quantity has one: the unit belongs to the table, not to the reader of a
    /// query. Counter and status values are Int16 in the database and would overflow at an
    /// odometer beyond 32767 km, so neither of those was an option.
    /// </summary>
    public record InfluxDistanceRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required decimal Value_Km { get; init; } // in km
        public InfluxDistanceRecord()
        {
            MeasurementType = MeasurementType.Distance;
        }
    }

    /// <summary>
    /// A geographic position. Two fields of one point rather than two measurements, because a
    /// latitude without its longitude is not half a position — it is nothing.
    /// </summary>
    public record InfluxPositionRecord : InfluxRecord
    {
        public required string SubCategory { get; init; }
        public required double Value_Latitude { get; init; }
        public required double Value_Longitude { get; init; }
        public InfluxPositionRecord()
        {
            MeasurementType = MeasurementType.Position;
        }
    }
}
