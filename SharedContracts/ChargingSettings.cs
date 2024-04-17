using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public class ChargingSettings
    {
        /// <summary>
        /// The configured charging profile
        /// 0 = off
        /// 1 = Access charging with battery priority
        /// 2 = Access charging with car priority
        /// 3 = Charging with PV power using battery power if needed
        /// 4 = Charging with PV power using grid power if needed
        /// 5 = Quick charging with 8 kW
        /// </summary>
        public int ChargingLevel { get; set; }

        /// <summary>
        /// The charging station that should be used first if both are available
        /// None-prefered station is used if the available power exceeds the minimum charging power of the first station (6A)
        /// </summary>
        public ChargingStation PreferedChargingStation { get; set; }

    };

    public enum ChargingStation
    {
        None = 0,
        Inside,
        Outside
    }
}
