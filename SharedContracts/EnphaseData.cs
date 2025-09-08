using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record EnphaseData(
        DateTimeOffset LastDataUpdate,
        int BatteryLevel, // Percent
        decimal BatteryEnergy, // in Wh
        decimal PowerFromPV, // in mW
        decimal PowerFromBattery, // in mW, negative when charging
        decimal PowerFromGrid, // in mW, negative when feeding into grid
        decimal PowerToHouse // in mW
    )
    {
        public const MeasurementCategory category = MeasurementCategory.Electricity;
        public const string sensorType = "Envoy";
    }
}
