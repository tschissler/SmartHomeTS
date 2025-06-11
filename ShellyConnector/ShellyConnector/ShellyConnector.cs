using Newtonsoft.Json;
using ShellyConnector.DataContracts;

namespace ShellyConnector
{
    public class ShellyConnector
    {
        public static ShellyPowerStatus? GetPowerStaus(ShellyDevice device)
        {
            ShellyPowerStatus? status = null;
            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    var jsonString = Http.GetStringAsync($"http://{device.IPAddress}/shelly").Result;
                    status = JsonConvert.DeserializeObject<ShellyPowerStatus>(jsonString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read data from Shelly device {device.IPAddress}, Error: " + ex.Message);
            }

            return status;
        }

        public static ShellyThermostatStatus? GetThermostatStaus(ShellyDevice device)
        {
            ShellyThermostatStatus? status = null;
            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    var jsonString = Http.GetStringAsync($"http://{device.IPAddress}/rpc/BluTrv.GetStatus?id={device.DeviceId}").Result;
                    status = JsonConvert.DeserializeObject<ShellyThermostatStatus>(jsonString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read data from Shelly device {device.IPAddress}, device id {device.DeviceId}, Error: " + ex.Message);
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
                                Timestamp: DateTimeOffset.Now,
                                IsValid: emeterData.Emeters[0].Is_Valid
                                );
                            break;
                        case DeviceType.ShellyPlugS:
                        case DeviceType.ShellyPlug2:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/meter/0").Result;
                            var meterData = JsonConvert.DeserializeObject<ShellyPlugMeterData>(jsonString);
                            powerData = new ShellyPowerData(
                                Power: meterData.Power,
                                TotalPower: meterData.Total,
                                Voltage: 0,
                                Timestamp: DateTimeOffset.Now,
                                IsValid: meterData.Is_Valid
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
                                Timestamp: DateTimeOffset.Now,
                                IsValid: true
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

        public static ShellyThermostatData? GetThermostatData(ShellyDevice device)
        {
            ShellyThermostatData? thermostatData = null;

            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    string jsonString;
                    switch (device.DeviceType)
                    {
                        case DeviceType.ShellyBluTRV:
                            var data = GetThermostatStaus(device);
                            thermostatData = new ShellyThermostatData(
                                TargetTemperature: data?.Target_C ?? 0,
                                CurrentTemperature: data?.Current_C ?? 0,
                                ValvePosition: data?.Pos ?? 0,
                                BatteryLevel: data?.Battery ?? 0,
                                IsConnected: data?.Connected ?? false,
                                LastUpdated: DateTimeOffset.FromUnixTimeSeconds(data?.Last_updated_ts ?? 0)
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

            return thermostatData;
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
