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
        private const string category = "Electricity";
        private const string sensorType = "Shelly";

        public IEnumerable<InfluxRecord> ToInfluxRecords()
        {
            var records = new List<InfluxRecord>
            {
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "Power",
                    Value = Power,
                    Unit = "W",
                    MeassurementType = MeassurementType.Power
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "Voltage",
                    Value = Voltage,
                    Unit = "V",
                    MeassurementType = MeassurementType.Voltage
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "TotalPower",
                    Value = TotalPower,
                    Unit = "KWh",
                    MeassurementType = MeassurementType.Energy
                }
            };
            return records;
        }
    }
}
