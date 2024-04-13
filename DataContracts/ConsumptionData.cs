using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataContracts
{
    public class ConsumptionData
    {
        public int HouseConsumption { get; set; }
        public int BatteryCharging { get; set; }
        public int GridFeed { get; set; }
        public int GarageCharging { get; set; }
        public int OutsideCharging { get; set; }
    }
}
