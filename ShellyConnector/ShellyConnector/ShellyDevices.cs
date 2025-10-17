using ShellyConnector.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector
{
    public class ShellyDevices
    {
        public static List<ShellyDevice> GetPowerDevices()
        {
            return new List<ShellyDevice>()
            {
                new ShellyDevice(
                    DeviceName : "Geschirrspüler",
                    DeviceType : DeviceType.ShellyPlus1PM,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 134]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Zirkulationspumpe",
                    DeviceType : DeviceType.ShellyPlus1PM,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 116]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Kaffeemaschine",
                    DeviceType : DeviceType.ShellyPlug2,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 85]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Kühlschrank",
                    DeviceType : DeviceType.ShellyPlusPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 96]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Herd",
                    DeviceType : DeviceType.Shelly3EM,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 179]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Hauswasserversorgung",
                    DeviceType : DeviceType.ShellyPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 191]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Waschmaschine",
                    DeviceType : DeviceType.ShellyPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 178]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Trockner",
                    DeviceType : DeviceType.ShellyPlusPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 142]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Warmwasser_M3",
                    DeviceType : DeviceType.ShellyPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 165]),
                    Location : Location.M3
                ),
                new ShellyDevice(
                    DeviceName : "Heizung_Bad_M3",
                    DeviceType : DeviceType.ShellyPlus1PM,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 198]),
                    Location : Location.M3
                ),
                new ShellyDevice(
                    DeviceName : "Lampe",
                    DeviceType : DeviceType.ShellyPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 177]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Fernseher",
                    DeviceType : DeviceType.ShellyPlusPlugS,
                    IPAddress : new System.Net.IPAddress([192, 168, 178, 57]),
                    Location : Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Wärmepumpe",
                    DeviceType: DeviceType.Shelly3EMGen3,
                    IPAddress: new System.Net.IPAddress([192, 168,178, 151]),
                    Location: Location.M1
                ),
                   new ShellyDevice(
                    DeviceName : "Wärmepumpe ST",
                    DeviceType: DeviceType.Shelly3EMGen3,
                    IPAddress: new System.Net.IPAddress([192, 168,178, 173]),
                    Location: Location.M1
                ),
                new ShellyDevice(
                    DeviceName : "Wärmepumpe",
                    DeviceType: DeviceType.Shelly3EMGen3,
                    IPAddress: new System.Net.IPAddress([192, 168,178, 43]),
                    Location: Location.M3
                ),
                   new ShellyDevice(
                    DeviceName : "Wärmepumpe ST",
                    DeviceType: DeviceType.Shelly3EMGen3,
                    IPAddress: new System.Net.IPAddress([192, 168,178, 119]),
                    Location: Location.M3
                ),   
               // new ShellyDevice(
               //     DeviceName : "Thermomix",
               //     DeviceType : DeviceType.ShellyPlusPlugS,
               //     IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 70 }),
               //     Location : Location.M1
               //)
            };
        }

        public static List<ShellyDevice> GetThermostatDevices()
        {
            return new List<ShellyDevice>()
            {
                // M1
                new ShellyDevice(
                    DeviceName : "Bad",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 74 }),
                    Location : Location.M1,
                    DeviceId : "200"
                ),

                //M3
                new ShellyDevice(
                    DeviceName : "Wohnzimmer",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 156 }),
                    Location : Location.M3,
                    DeviceId : "200"

                ),
                new ShellyDevice(
                    DeviceName : "Buero",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 156 }),
                    Location : Location.M3,
                    DeviceId : "201"
                ),
                new ShellyDevice(
                    DeviceName : "Gaestezimmer",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 156 }),
                    Location : Location.M3,
                    DeviceId : "202"
                ),
                new ShellyDevice(
                    DeviceName : "Esszimmer",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 156 }),
                    Location : Location.M3,
                    DeviceId : "203"
                ),
                new ShellyDevice(
                    DeviceName : "Buero_Thomas",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 156 }),
                    Location : Location.M3,
                    DeviceId : "204"
                ),
                new ShellyDevice(
                    DeviceName : "Kueche",
                    DeviceType : DeviceType.ShellyBluTRV,
                    IPAddress : new System.Net.IPAddress(new byte[] { 192, 168, 178, 156 }),
                    Location : Location.M3,
                    DeviceId : "205"
                )
            };
        }
    }
}
