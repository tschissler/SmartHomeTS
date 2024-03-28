using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ShellyLib
{
    public enum DeviceType { ShellyPlugS, ShellyPlusPlugS, ShellyPlus1PM, Shelly3EM }

    public class ShellyDevice
    {
        public DeviceType DeviceType { get; set; }
        public IPAddress IPAddress { get; set; }
    }
}
