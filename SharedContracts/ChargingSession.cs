using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record ChargingSession(
        int SessionId,
        DateTimeOffset? StartTime,
        DateTimeOffset? EndTime,
        double TatalEnergyAtStart,
        double EnergyOfChargingSession,
        string WallboxName
    );
}
