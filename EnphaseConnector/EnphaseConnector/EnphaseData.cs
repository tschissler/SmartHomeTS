using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnphaseConnector
{
    public record EnphaseData(
        DateTimeOffset LastDataUpdate,
        int BatteryLevel,
        int BatteryEnergy,
        long PowerFromPV,
        long PowerFromBattery,
        long PowerFromGrid,
        long PowerToHouse
    );
}
