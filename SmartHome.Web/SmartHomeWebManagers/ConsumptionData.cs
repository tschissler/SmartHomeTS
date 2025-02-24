using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartHomeWebManagers
{
    public class ConsumptionData
    {
        public decimal HouseConsumption { get; set; }
        public decimal BatteryCharging { get; set; }
        public decimal GridFeed { get; set; }
        public decimal GarageCharging { get; set; }
        public decimal OutsideCharging { get; set; }
    }
}
