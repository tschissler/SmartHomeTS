using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record EnphaseData(
        DateTimeOffset LastDataUpdate,
        decimal PowerFromPV, // in mW
        decimal PowerFromBattery, // in mW, negative when charging
        decimal PowerFromGrid, // in mW, negative when feeding into grid
        decimal PowerToHouse, // in mW
        int? BatteryLevel = null, // Percent, sent every ~60s
        decimal? BatteryEnergy = null, // in Wh, sent every ~60s
        decimal? EnergyFromPVLifetime = null, // in Wh, cumulative lifetime counter, sent every ~60s
        decimal? EnergyToHouseLifetime = null  // in Wh, cumulative lifetime counter, sent every ~60s
    )
    {
        public const MeasurementCategory category = MeasurementCategory.Electricity;
        public const string sensorType = "Envoy";
    }
}
