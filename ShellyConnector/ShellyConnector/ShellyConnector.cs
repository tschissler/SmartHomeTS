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
