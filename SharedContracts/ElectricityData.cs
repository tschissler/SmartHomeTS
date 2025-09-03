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
        private const string category = "Electricity";
        private const string sensorType = "Envoy";

        public IEnumerable<InfluxRecord> ToInfluxRecords()
        {
            var records = new List<InfluxRecord>
            {
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "BatteryLevel",
                    Value = BatteryLevel,
                    Unit = "Percent",
                    MeassurementType = MeassurementType.Percent
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "BatteryEnergy",
                    Value = BatteryEnergy,
                    Unit = "Wh",
                    MeassurementType = MeassurementType.Energy
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "PowerFromPV",
                    Value = PowerFromPV,
                    Unit = "W",
                    MeassurementType = MeassurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "PowerFromBattery",
                    Value = PowerFromBattery,
                    Unit = "W",
                    MeassurementType = MeassurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "PowerFromGrid",
                    Value = PowerFromGrid,
                    Unit = "W",
                    MeassurementType = MeassurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "PowerToHouse",
                    Value = PowerToHouse,
                    Unit = "W",
                    MeassurementType = MeassurementType.Power
                }
            };
            return records;
        }
    }
}
