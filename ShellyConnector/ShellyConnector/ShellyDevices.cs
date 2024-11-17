using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector
{
    public class ShellyDevices
    {
        public static List<ShellyDevice> GetDevices()
        {
            return new List<ShellyDevice>()
            {
                new ShellyDevice()
                {
                    DeviceName = "Geschirrspüler",
                    DeviceType = DeviceType.ShellyPlus1PM,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 120 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Zirkulationspumpe",
                    DeviceType = DeviceType.ShellyPlus1PM,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 116 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Kaffeemaschine",
                    DeviceType = DeviceType.ShellyPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 84 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Kühlschrank",
                    DeviceType = DeviceType.ShellyPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 96 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Herd",
                    DeviceType = DeviceType.Shelly3EM,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 179 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Hauswasserversorgung",
                    DeviceType = DeviceType.ShellyPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 191 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Waschmaschine",
                    DeviceType = DeviceType.ShellyPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 178 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Trockner",
                    DeviceType = DeviceType.ShellyPlusPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 114 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Wärmepumpe_M3",
                    DeviceType = DeviceType.ShellyPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 165 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Heizung_Bad_M3",
                    DeviceType = DeviceType.ShellyPlus1PM,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 198 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Lampe",
                    DeviceType = DeviceType.ShellyPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 177 })
                },
                new ShellyDevice()
                {
                    DeviceName = "WLAN-Router",
                    DeviceType = DeviceType.ShellyPlusPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 23 })
                },
                new ShellyDevice()
                {
                    DeviceName = "Fernseher",
                    DeviceType = DeviceType.ShellyPlusPlugS,
                    IPAddress = new System.Net.IPAddress(new byte[] { 192, 168, 178, 57 })
                }
            };
        }
    }
}
