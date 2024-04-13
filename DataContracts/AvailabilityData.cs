using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataContracts
{
    public class AvailabilityData
    {
        public int PVPowerPercent { get; set; }
        public int BatteryPowerPercent { get; set; }
        public int GridPowerPercent { get; set; }
    }
}
