using Newtonsoft.Json;
using ShellyConnector.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector
{
    public class ShellyConnector
    {
        public static ShellyStatus? GetStaus(ShellyDevice device)
        {
            ShellyStatus? status = null;
            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    var jsonString = Http.GetStringAsync($"http://{device.IPAddress}/shelly").Result;
                    status = JsonConvert.DeserializeObject<ShellyStatus>(jsonString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read data from Shelly device {device.IPAddress}, Error: " + ex.Message);
            }

            return status;
        }

        public static ShellyPowerData? GetPowerData(ShellyDevice device)
        {
            ShellyPowerData? powerData = null;
            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    string jsonString;
                    switch (device.DeviceType)
                    {
                        case DeviceType.Shelly3EM:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/status").Result;
                            var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonString);
                            var emetersJson = jsonObject.emeters.ToString();
                            var emeterData = JsonConvert.DeserializeObject<ShellyEmeterData>($"{{\"Emeters\": {emetersJson}}}");
                            powerData = new ShellyPowerData(
                                Power: emeterData.Emeters[0].Power + emeterData.Emeters[1].Power + emeterData.Emeters[2].Power,
                                TotalPower: emeterData.Emeters[0].Total + emeterData.Emeters[1].Total + emeterData.Emeters[2].Total,
                                Voltage: (emeterData.Emeters[0].Voltage + emeterData.Emeters[0].Voltage + emeterData.Emeters[0].Voltage) / 3,
                                Timestamp: DateTimeOffset.Now
                                );
                            break;
                        case DeviceType.ShellyPlugS:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/meter/0").Result;
                            var meterData = JsonConvert.DeserializeObject<ShellyPlugMeterData>(jsonString);
                            powerData = new ShellyPowerData(
                                Power: meterData.Power,
                                TotalPower: meterData.Total,
                                Voltage: 0,
                                Timestamp: DateTimeOffset.Now
                                );
                            break;
                        case DeviceType.ShellyPlusPlugS:
                        case DeviceType.ShellyPlus1PM:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/rpc/Shelly.GetStatus").Result;
                            var plugPlusMeterData = JsonConvert.DeserializeObject<ShellyPlugPlusMeterData>(jsonString);
                            powerData = new ShellyPowerData(
                                Power: plugPlusMeterData.Switch0.apower,
                                TotalPower: plugPlusMeterData.Switch0.aenergy.total,
                                Voltage: plugPlusMeterData.Switch0.voltage,
                                Timestamp: DateTimeOffset.Now
                                );
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read meter data from Shelly device {device.IPAddress} type {device.DeviceType.ToString()}, Error: " + ex.Message);
            }

            return powerData;
        }

        public static void SetRelay(ShellyDevice device, string state)
        {
            state = state.ToLower();
            if (state != "on" && state != "off" && state != "toggle")
            {
                Console.WriteLine($"Invalid state {state} for relay on device {device.IPAddress}");
                return;
            }
            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    var jsonString = Http.GetStringAsync($"http://{device.IPAddress}/relay/0?turn={state}").Result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set relay on device {device.IPAddress}, Error: " + ex.Message);
            }
        }
    }
}
