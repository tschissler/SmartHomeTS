using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record EnphaseData(
        DateTimeOffset LastDataUpdate,
        int BatteryLevel,
        int BatteryEnergy,
        decimal PowerFromPV,
        decimal PowerFromBattery,
        decimal PowerFromGrid,
        decimal PowerToHouse
    )
    {
        private const DataCategory category = DataCategory.Electricity;
        private const string sensorType = "Envoy";

        public IEnumerable<InfluxRecord> ToInfluxRecords()
        {
            var records = new List<InfluxRecord>
            {
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = SubCategory.Other,
                    SensorType = sensorType,
                    Measurement = "BatteryLevel",
                    Value = BatteryLevel,
                    Unit = "Percent",
                    MeasurementType = MeasurementType.Percent
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = SubCategory.Other,
                    SensorType = sensorType,
                    Measurement = "BatteryEnergy",
                    Value = BatteryEnergy,
                    Unit = "Wh",
                    MeasurementType = MeasurementType.Energy
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = SubCategory.Production,
                    SensorType = sensorType,
                    Measurement = "PowerFromPV",
                    Value = PowerFromPV,
                    Unit = "W",
                    MeasurementType = MeasurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = PowerFromBattery < 0 ? SubCategory.Consumption : SubCategory.Production,
                    SensorType = sensorType,
                    Measurement = "PowerFromBattery",
                    Value = PowerFromBattery,
                    Unit = "W",
                    MeasurementType = MeasurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = PowerFromGrid < 0 ? SubCategory.Consumption : SubCategory.Production,
                    SensorType = sensorType,
                    Measurement = "PowerFromGrid",
                    Value = PowerFromGrid,
                    Unit = "W",
                    MeasurementType = MeasurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = SubCategory.Consumption,
                    SensorType = sensorType,
                    Measurement = "PowerToHouse",
                    Value = PowerToHouse,
                    Unit = "W",
                    MeasurementType = MeasurementType.Power
                }
            };
            return records;
        }
    }
}
