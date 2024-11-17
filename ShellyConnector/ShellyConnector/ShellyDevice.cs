using System.Net;

namespace ShellyConnector
{
    public enum DeviceType { ShellyPlugS, ShellyPlusPlugS, ShellyPlus1PM, Shelly3EM }

    public class ShellyDevice
    {
        public string DeviceName { get; set; }
        public DeviceType DeviceType { get; set; }
        public IPAddress IPAddress { get; set; }
    }
}
