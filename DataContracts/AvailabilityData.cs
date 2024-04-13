using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataContracts
{
    public class AvailabilityData
    {
        public decimal PVPowerPercent { get; set; }
        public decimal BatteryPowerPercent { get; set; }
        public decimal GridPowerPercent { get; set; }
    }
}
