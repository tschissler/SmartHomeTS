using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedContracts;
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
        private readonly SemaphoreSlim udpSemaphore = new(1, 1);
        private int LastChargingSessionPublishedViaMQTT = 0;

        public KebaDeviceConnector(IPAddress IpAddress, int UDPPort, object? lockObject = null)
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
            string? writeToDeviceFlag = Environment.GetEnvironmentVariable("KEBA_WRITE_TO_DEVICE");
            if (writeToDeviceFlag == null || writeToDeviceFlag.ToLower() != "true")
            {
                Console.WriteLine("Environment variable KEBA_WRITE_TO_DEVICE is not set to 'true', so we will not write to the device");
                Console.WriteLine($"Would have written {newCurrent} mA as charging current to device otherwise");
                return;
            }
            if (newCurrent == previousChargingCurrencyWrittenToDevice)
            {
                Console.WriteLine($"Charging current already set to {newCurrent} mA, skipping write to device");
                return;
            }
            WriteChargingCurrentToDevice(newCurrent);
            return;
        }

        public async Task<KebaData?> ReadDeviceData()
        {
            if (!await udpSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                Console.WriteLine("Other UDP operation is still in progress, skipping read from device.");
                return null;
            }
            try
            {
                var data = GetDeviceStatus();
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
                    data.CurrentChargingPower / 1000,
                    data.EnergyCurrentChargingSession / 10,
                    data.EnergyTotal / 10
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read data from Keba device, Error: {ex.Message}");
            }
            finally
            {
                udpSemaphore.Release();
            }

            return null;
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


        public List<(int report, KebaReportData? session)> ReadReports()
        {
            return [
                (100, JsonConvert.DeserializeObject<KebaReportData>(GetDeviceReport(100))), 
                (101, JsonConvert.DeserializeObject<KebaReportData>(GetDeviceReport(101))), 
                (102, JsonConvert.DeserializeObject<KebaReportData>(GetDeviceReport(102))),
                (103, JsonConvert.DeserializeObject<KebaReportData>(GetDeviceReport(103)))
                ];
        }

        public async Task<ChargingSession?> CheckIfChargingSessionEnded(string wallbox)
        {
            var currentChargingSession = ReadReport(101, wallbox);
            if (currentChargingSession is not null
                && currentChargingSession.EndTime is not null 
                && LastChargingSessionPublishedViaMQTT != currentChargingSession.SessionId)
            {
                LastChargingSessionPublishedViaMQTT = currentChargingSession.SessionId;
                return currentChargingSession;
            }
            return null;
        }

        private ChargingSession? ReadReport(int reportId, string wallbox)
        {
            if (!udpSemaphore.Wait(TimeSpan.FromSeconds(5)))
            {
                Console.WriteLine("Other UDP operation is still in progress, skipping report read from device.");
                return null;
            }
            try
            {
                var data = JsonConvert.DeserializeObject<KebaReportData>(GetDeviceReport(reportId));
                if (data == null)
                {
                    Console.WriteLine("Failed to read data from Keba device");
                    return null;
                }
                return new ChargingSession
                (
                    SessionId: data.SessionID,
                    StartTime: ParseDateTimeOffset(data.Started),
                    EndTime: ParseDateTimeOffset(data.Ended),
                    TatalEnergyAtStart: data.Estart / 10.0,
                    EnergyOfChargingSession: data.Epres / 10.0,
                    WallboxName: wallbox,
                    ChargedCar: ""
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read charging session from Keba device, Error: {ex.Message}");
            }
            finally
            {
                udpSemaphore.Release();
            }
            return null;
        }

        internal DateTimeOffset? ParseDateTimeOffset(string? input)
        {
            if (input is null || input == "0")
                return null;

            if (DateTimeOffset.TryParseExact(input, "yyyy-MM-dd HH:mm:ss.fff",
                                             System.Globalization.CultureInfo.InvariantCulture,
                                             System.Globalization.DateTimeStyles.None,
                                             out var result))
            {
                return result;
            }
            throw new FormatException("Input string is not in the correct format: YYYY-MM-DD hh:mm:ss,000");
        }

        internal string GetDeviceReport(int reportId)
        {
            Task.Delay(500).Wait(); // Wait for 500ms to avoid UDP command collision
            return ExecuteUDPCommand($"report {reportId}");
        }

        private void WriteChargingCurrentToDevice(int current)
        {
            if (!udpSemaphore.Wait(TimeSpan.FromSeconds(5)))
            {
                Console.WriteLine("Other UDP operation is still in progress, skipping write to device.");
                return;
            }
            try
            {
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    Console.WriteLine($"Updating charging current to {current} (attempt {attempt}/{maxRetries})");
                    var result = ExecuteUDPCommand($"currtime {current} 1");
                    Console.WriteLine("Result: " + result);
                    if (result == "TCH-OK :done\n")
                    {
                        Console.WriteLine("Updated charging current to " + current);
                        previousChargingCurrencyWrittenToDevice = current;
                        return;
                    }

                    // Check if we received a report response instead of the expected TCH-OK
                    // This happens when UDP responses from concurrent commands get mixed up
                    if (result.TrimStart().StartsWith("{"))
                    {
                        Console.WriteLine($"Received unexpected report response instead of TCH-OK, retrying...");
                        Thread.Sleep(500);
                        continue;
                    }

                    Console.WriteLine($"Setting charging current failed - unexpected response: {result}");
                    break;
                }
                Console.WriteLine($"Failed to set charging current to {current} after {maxRetries} attempts");
            }
            finally
            {
                udpSemaphore.Release();
            }
        }

        internal KebaDeviceStatusData GetDeviceStatus()
        {
            
            var dataString = "";
            var data = new KebaDeviceStatusData();
            try
            {
                // Report 2 Data
                //{
                //  "ID": "2",
                //  "State": 5,
                //  "Error1": 0,
                //  "Error2": 0,
                //  "Plug": 7,
                //  "AuthON": 0,
                //  "Authreq": 0,
                //  "Enable sys": 0,
                //  "Enable user": 0,
                //  "Max curr": 0,
                //  "Max curr %": 1000,
                //  "Curr HW": 16000,
                //  "Curr user": 6000,
                //  "Curr FS": 0,
                //  "Tmo FS": 0,
                //  "Curr timer": 0,
                //  "Tmo CT": 0,
                //  "Setenergy": 0,
                //  "Output": 0,
                //  "Input": 0,
                //  "X2 phaseSwitch source": 0,
                //  "X2 phaseSwitch": 0,
                //  "Serial": "22588720",
                //  "Sec": 44829
                //}
                var report2 = ExecuteUDPCommand("report 2");

                // Report 3 data
                //{
                //  "ID": "3",
                //  "U1": 0,
                //  "U2": 0,
                //  "U3": 0,
                //  "I1": 0,
                //  "I2": 0,
                //  "I3": 0,
                //  "P": 0,
                //  "PF": 0,
                //  "E pres": 164170,
                //  "E total": 94751800,
                //  "Serial": "22588720",
                //  "Sec": 45306
                //}
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