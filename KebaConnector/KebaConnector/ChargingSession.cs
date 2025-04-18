using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KebaConnector
{
    public class ChargingSession
    {
        public int SessionID { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public double TatalEnergyAtStart { get; set; }
        public double EnergyOfChargingSession { get; set; }
    }
}
