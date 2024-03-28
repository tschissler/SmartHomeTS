using Newtonsoft.Json;
using System.Net;

namespace ShellyLib
{
    public class ShellyConnector
    {
        public static double ReadPower(ShellyDevice device)
        {
            double value = 0;
            try
            {
                using (HttpClient Http = new HttpClient())
                {
                    string jsonString;

                    switch (device.DeviceType)
                    {
                        case DeviceType.ShellyPlugS:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/meter/0").Result;
                            value = JsonConvert.DeserializeObject<ShellyPlugMeterData>(jsonString).power;
                            break;
                        case DeviceType.ShellyPlusPlugS:
                        case DeviceType.ShellyPlus1PM:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/rpc/Switch.GetStatus?id=0").Result;
                            value = JsonConvert.DeserializeObject<ShellyPlusPlugMeterData>(jsonString).apower;
                            break;
                        case DeviceType.Shelly3EM:
                            jsonString = Http.GetStringAsync($"http://{device.IPAddress}/status").Result;
                            value = JsonConvert.DeserializeObject<EM3Data>(jsonString).total_power;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read data from Shelly device {device.IPAddress}, Error: " + ex.Message);
            }
            return value;
        }

        public static bool TurnRelayOn(ShellyDevice device)
        {
            return TurnRelay(device.IPAddress, true);
        }

        public static bool TurnRelayOff(ShellyDevice device)
        {
            return TurnRelay(device.IPAddress, false);
        }

        private static bool TurnRelay(IPAddress deviceAddress, bool turnOn)
        {
            bool value = false;
            try
            {
                var turnString = turnOn ? "on" : "off";

                using (HttpClient Http = new HttpClient())
                {
                    var jsonString = Http.GetStringAsync($"http://{deviceAddress}/relay/0?turn={turnString}").Result;
                    value = JsonConvert.DeserializeObject<TurnOnOffResponse>(jsonString).ison;
                }
                if (value != turnOn)
                {
                    throw new Exception($"Turning relay {turnString} did not succeed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to turn on relay on Shelly device {deviceAddress}, Error: " + ex.Message);
            }
            return value;
        }
    }

    public class ShellyPlugMeterData
    {
        public double power { get; set; }
        public bool is_valid { get; set; }
        public double overpower { get; set; }
        public double timestamp { get; set; }
        public List<double> counters { get; set; }
        public double number { get; set; }
    }


    public class ShellyPlusPlugMeterData
    {
        public int id { get; set; }
        public string source { get; set; }
        public bool output { get; set; }
        public float apower { get; set; }
        public float voltage { get; set; }
        public float current { get; set; }
        public Aenergy aenergy { get; set; }
    }

    public class Aenergy
    {
        public float total { get; set; }
        public float[] by_minute { get; set; }
        public int minute_ts { get; set; }
    }

    public class EM3Data
    {
        public string time { get; set; }
        public int unixtime { get; set; }
        public int serial { get; set; }
        public bool has_update { get; set; }
        public string mac { get; set; }
        public int cfg_changed_cnt { get; set; }
        public Emeter[] emeters { get; set; }
        public double total_power { get; set; }
    }

    public class Emeter
    {
        public float power { get; set; }
        public float pf { get; set; }
        public float current { get; set; }
        public float voltage { get; set; }
        public bool is_valid { get; set; }
        public float total { get; set; }
        public float total_returned { get; set; }
    }


    public class TurnOnOffResponse
    {
        public bool ison { get; set; }
        public bool has_timer { get; set; }
        public int timer_started { get; set; }
        public int timer_duration { get; set; }
        public int timer_remaining { get; set; }
        public bool overpower { get; set; }
        public string source { get; set; }
    }

}