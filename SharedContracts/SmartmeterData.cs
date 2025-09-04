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
        private const DataCategory category = DataCategory.Electricity;
        private const string sensorType = "SMLSensor";

        public IEnumerable<InfluxRecord> ToInfluxRecords()
        {
            var records = new List<InfluxRecord>
            {
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = SubCategory.Production,
                    SensorType = sensorType,
                    Measurement = "GridConsumption",
                    Value = Netzbezug,
                    Unit = "KWh",
                    MeasurementType = MeasurementType.Energy
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = SubCategory.Consumption,
                    SensorType = sensorType,
                    Measurement = "GridSupply",
                    Value = Netzeinspeisung,
                    Unit = "KWh",
                    MeasurementType = MeasurementType.Energy
                },
                new InfluxRecord
                {
                    Category = category,
                    SubCategory = NetzanschlussMomentanleistung < 0 ? SubCategory.Consumption : SubCategory.Production,
                    SensorType = sensorType,
                    Measurement = "CurrentPower",
                    Value = NetzanschlussMomentanleistung,
                    Unit = "KWh",
                    MeasurementType = MeasurementType.Power
                }
            };
            return records;
        }
    }
}