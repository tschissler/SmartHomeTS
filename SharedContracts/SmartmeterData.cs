using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record SmartmeterData
    (
        decimal Netzbezug,
        decimal Netzeinspeisung,
        decimal NetzanschlussMomentanleistung
    )
    {
        private const string category = "Electricity";
        private const string sensorType = "SMLSensor";

        public IEnumerable<InfluxRecord> ToInfluxRecords()
        {
            var records = new List<InfluxRecord>
            {
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "GridConsumption",
                    Value = Netzbezug,
                    Unit = "KWh",
                    MeassurementType = MeassurementType.Energy
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "GridSupply",
                    Value = Netzeinspeisung,
                    Unit = "KWh",
                    MeassurementType = MeassurementType.Energy
                },
                new InfluxRecord
                {
                    Category = category,
                    SensorType = sensorType,
                    Meassurement = "CurrentPower",
                    Value = NetzanschlussMomentanleistung,
                    Unit = "KWh",
                    MeassurementType = MeassurementType.Power
                }
            };
            return records;
        }
    }
}