using System.Net;

namespace ShellyConnector.DataContracts
{
    public enum DeviceType { ShellyPlugS, ShellyPlusPlugS, ShellyPlus1PM, Shelly3EM }
    public enum Location { M1, M3 }

    public record ShellyDevice(
        string DeviceName,
        DeviceType DeviceType,
        IPAddress IPAddress,
        Location Location
    );
}
