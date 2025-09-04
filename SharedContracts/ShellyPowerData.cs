using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record ShellyPowerData(
        DateTimeOffset Timestamp,
        decimal Power,
        decimal Voltage,
        decimal TotalPower
    )
    {
        private const DataCategory category = DataCategory.Electricity;
        private const string sensorType = "Shelly";
        private const SubCategory subCategory = SubCategory.Consumption;

        public IEnumerable<InfluxRecord> ToInfluxRecords()
        {
            var records = new List<InfluxRecord>
            {
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = subCategory,
                    SensorType = sensorType,
                    Measurement = "Power",
                    Value = Power,
                    Unit = "W",
                    MeasurementType = MeasurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = subCategory,
                    SensorType = sensorType,
                    Measurement = "Voltage",
                    Value = Voltage,
                    Unit = "V",
                    MeasurementType = MeasurementType.Voltage
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = subCategory,
                    SensorType = sensorType,
                    Measurement = "TotalPower",
                    Value = TotalPower,
                    Unit = "KWh",
                    MeasurementType = MeasurementType.Energy
                }
            };
            return records;
        }
    }
}
