using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KebaConnector
{
    public class KebaDeviceConnector
    {
        private IPAddress ipAddress;
        private int uDPPort;
        private int previousChargingCurrencyWrittenToDevice = 0;
        private bool udpExceptionInProgress = false;

        public KebaDeviceConnector(IPAddress IpAddress, int UDPPort, object lockObject = null)
        {
            ipAddress = IpAddress;
            uDPPort = UDPPort;
        }

        /// <summary>
        /// Writes new charging currency to the device and reads the current status of the device.
        /// </summary>
        /// <remarks>
        /// As the Keba documentation states there should be a 2 second delay between sending commands to the device, 
        /// this method has a sleep between writing and reading.
        /// The method writes new data to the device only if the new data is different from the previous one.
        /// </remarks>
        /// <param name="state"></param>
        public async Task UpdateDeviceDesiredCurrent(int newCurrent)
        {
            if (Environment.GetEnvironmentVariable("KEBA_WRITE_TO_DEVICE") == null || Environment.GetEnvironmentVariable("KEBA_WRITE_TO_DEVICE").ToLower() != "true")
            {
                Console.WriteLine("Environment variable KEBA_WRITE_TO_DEVICE is not set to 'true', so we will not write to the device");
                Console.WriteLine($"Would have written {newCurrent} mA as charging current to device otherwise");
                return;
            }
            WriteChargingCurrentToDevice(newCurrent);
            return;
        }

        public async Task<KebaData> ReadDeviceData()
        {
            if (udpExceptionInProgress)
            {
                throw new Exception("Other UDP Operation is still in progress, skipping read from device. This might be du to failing UDP communication with device.");
            }
            udpExceptionInProgress = true;
            KebaDeviceStatusData data;
            try
            {
                data = GetDeviceStatus();
                udpExceptionInProgress = false;
                return new KebaData(
                    (PlugStatus)data.PlugStatus,
                    data.ChargingEnabled == 1,
                    data.DeviceEnabled == 1,
                    data.MaxCurrency,
                    data.MaxcurrPercent,
                    data.CurrencySupportedByDevice,
                    data.TargetCurrency,
                    data.TargetEnergy,
                    data.Serial,
                    data.VoltagePhase1,
                    data.VoltagePhase2,
                    data.VoltagePhase3,
                    data.CurrentPhase1,
                    data.CurrentPhase2,
                    data.CurrentPhase3,
                    data.CurrentChargingPower,
                    data.EnergyCurrentChargingSession,
                    data.EnergyTotal
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read data from Keba device, Error: {ex.Message}");
                udpExceptionInProgress = false;
            }

            return null;

            //if (data != null)
            //{
            //    DataPoints.CurrentChargingPower.SetCorrectedValue(data.CurrentChargingPower);
            //    DataPoints.EffectiveMaximumChargingCurrency.SetCorrectedValue(data.MaxCurrency);
            //    DataPoints.CurrentVoltage.SetCorrectedValue((data.VoltagePhase1 + data.VoltagePhase2 + data.VoltagePhase3) / 3);
            //    DataPoints.ConsumptionActiveSession.SetCorrectedValue(data.EnergyCurrentChargingSession);
            //    DataPoints.CharingOverallTotal.SetCorrectedValue(data.EnergyTotal);
            //    DataPoints.KebaStatus.CurrentValue = data.State;

            //    //Console.WriteLine($"Active Session: {DataPoints.ConsumptionActiveSession.AssembleValueString()}, " +
            //    //    $"Total: {DataPoints.CharingOverallTotal.AssembleValueString()}, "+
            //    //    $"Power: {DataPoints.CurrentChargingPower.AssembleValueString()}, " +
            //    //    $"Max: {DataPoints.EffectiveMaximumChargingCurrency.AssembleValueString()}, " +
            //    //    $"State: {DataPoints.KebaStatus.CurrentValue}");
            //}

            //if (previousChargingCurrencyWrittenToDevice != DataPoints.AdjustedCharingCurrency.CurrentValue &&
            //    (Environment.GetEnvironmentVariable("KEBA_WRITE_TO_DEVICE") == null ||
            //    Environment.GetEnvironmentVariable("KEBA_WRITE_TO_DEVICE").ToLower() != "false"))
            //{
            //    try
            //    {
            //        previousChargingCurrencyWrittenToDevice = DataPoints.AdjustedCharingCurrency.CurrentValue;
            //        WriteChargingCurrentToDevice((int)(DataPoints.AdjustedCharingCurrency.CurrentValue * 1000));
            //        Console.WriteLine($"Wrote {DataPoints.AdjustedCharingCurrency.CurrentValue} to device as new target charging currency (currtime)");
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.Writeline($"Failed to write {DataPoints.AdjustedCharingCurrency.CurrentValue} to device as new target charging currency (currtime). {ex.Message}");
            //    }
            //}
        }

        internal string GetDeviceInformation()
        {
            return ExecuteUDPCommand("i");
        }

        internal string GetDeviceReport1()
        {
            return ExecuteUDPCommand("report 1");
        }

        internal string GetDeviceReport2()
        {
            return ExecuteUDPCommand("report 2");
        }

        private void WriteChargingCurrentToDevice(int current)
        {
            Console.WriteLine("Updating charging currency to " + current);
            var success = ExecuteUDPCommand($"currtime {current} 1");
            Console.WriteLine("Result: " + success);
            if (success != "TCH-OK :done\n")
            {
                Console.WriteLine($"Setting currency failed - {success}");
                throw new Exception($"Setting currency failed");
            }
            else
            {
                Console.WriteLine("Updated charging currency to " + current);
            }
        }

        internal KebaDeviceStatusData GetDeviceStatus()
        {
            
            var dataString = "";
            var data = new KebaDeviceStatusData();
            try
            {
                var report2 = ExecuteUDPCommand("report 2");
                var report3 = ExecuteUDPCommand("report 3");
                dataString = report2 + report3;
                JObject report2Json = JObject.Parse(report2);
                JObject report3Json = JObject.Parse(report3);

                report2Json.Merge(report3Json, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union
                });

                data = JsonConvert.DeserializeObject<KebaDeviceStatusData>(report2Json.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading device status: {ex.Message}\n {dataString}");
            }
            return data;
        }

        internal string ExecuteUDPCommand(string command)
        {
            string result = "";
            using (UdpClient udpClient = new UdpClient())
            {
                try
                {
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, uDPPort));
                    udpClient.Connect(ipAddress, uDPPort);

                    // Sends a message to the host to which you have connected.
                    byte[] sendBytes = Encoding.ASCII.GetBytes(command);

                    udpClient.Send(sendBytes, sendBytes.Length);

                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(ipAddress, 0);

                    // Blocks until a message returns on this socket from a remote host.
                    byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    string returnData = Encoding.ASCII.GetString(receiveBytes);

                    result = returnData.ToString();
                    Thread.Sleep(200);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while communicating via UDP with Keba device: " + e.Message);
                }
            }
            return result;
        }
    }
}