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
        public const MeasurementCategory Category = MeasurementCategory.Electricity;
        public const string SensorType = "SMLSensor";
    }
}