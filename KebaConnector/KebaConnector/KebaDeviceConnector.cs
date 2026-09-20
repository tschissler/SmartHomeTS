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
        // Shared across all instances: both wallboxes answer to local port 7090, so concurrent
        // commands to different boxes can receive each other's responses. Serialize all UDP traffic.
        private static readonly SemaphoreSlim udpSemaphore = new(1, 1);

        // Session tracking: the plug-in edge this connector observed itself is the trustworthy
        // source for the session start, the clock of the box is only the fallback.
        // Null until the first read cycle: at startup the previous plug state is unknown, not
        // "not connected". Treating it as not connected would turn the first reading of an
        // already charging car into an observed plug-in edge and report the restart time as
        // the session start — wrong, and flagged as trustworthy while being so.
        private bool? vehicleWasConnected;
        private DateTimeOffset? plugInObservedAt;

        // Last value successfully written to the device via currtime (-1 = nothing written yet)
        private volatile int lastWrittenCurrent = -1;
        // Last desired current received from the ChargingController and when the controller
        // decided it. The wallbox forgets its current limit when a charging session ends, so
        // the desired value is re-applied as long as it is fresh (controller heartbeats every
        // 60s). The age comes from the Zeitpunkt in the payload, never from the arrival time:
        // the command is retained, so the broker replays it on every subscribe and an
        // arbitrarily old setpoint would look brand new. See UpdateDeviceDesiredCurrent.
        private volatile int desiredCurrent = -1;
        private DateTimeOffset desiredCurrentSentAt;
        private DateTimeOffset lastEnforcementWriteAt = DateTimeOffset.MinValue;
        private bool staleReleaseDone = false;
        private readonly TimeProvider time;
        private static readonly TimeSpan SetpointFreshDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan StaleReleaseAfter = TimeSpan.FromMinutes(10);
        // Written when the controller goes silent: the box caps at its hardware limit (Curr HW),
        // so the car can always charge even if the control loop is down.
        internal const int ReleaseCurrent = 63000;

        public KebaDeviceConnector(IPAddress IpAddress, int UDPPort, object? lockObject = null, TimeProvider? timeProvider = null)
        {
            ipAddress = IpAddress;
            uDPPort = UDPPort;
            time = timeProvider ?? TimeProvider.System;
            // Nothing has been commanded yet. Counting the age from the start of the connector
            // gives the controller StaleReleaseAfter to show up before the box is released.
            desiredCurrentSentAt = time.GetUtcNow();
        }

        protected virtual bool WritesEnabled()
        {
            string? writeToDeviceFlag = Environment.GetEnvironmentVariable("KEBA_WRITE_TO_DEVICE");
            return writeToDeviceFlag != null && writeToDeviceFlag.ToLower() == "true";
        }

        /// <summary>
        /// Writes new charging currency to the device and reads the current status of the device.
        /// </summary>
        /// <remarks>
        /// As the Keba documentation states there should be a 2 second delay between sending commands to the device, 
        /// this method has a sleep between writing and reading.
        /// The method writes new data to the device only if the new data is different from the previous one.
        /// </remarks>
        /// <param name="newCurrent">Desired charging current in mA.</param>
        /// <param name="sentAt">
        /// The Zeitpunkt from the command payload: when the ChargingController decided this
        /// setpoint, not when the message arrived. The command is published retained, so the
        /// broker replays it on every subscribe — on connector startup and on every reconnect.
        /// Taking the arrival time as the age restarts the freshness clock on each replay, and
        /// StaleReleaseAfter — the emergency release that keeps charging possible without the
        /// control loop — never fires; the box then stays stuck on an arbitrarily old setpoint,
        /// permanently switched off if that setpoint was 0 mA. Writing the value is left to
        /// EnforceDesiredState, which checks the age first, so a setpoint that is already stale
        /// never reaches the box.
        /// </param>
        public async Task UpdateDeviceDesiredCurrent(int newCurrent, DateTimeOffset sentAt)
        {
            desiredCurrent = newCurrent;

            var now = time.GetUtcNow();
            if (sentAt > now)
            {
                // Clock skew between the two pods would otherwise make this setpoint immortal:
                // an age that never grows can never reach StaleReleaseAfter.
                Console.WriteLine($"Charging command of {newCurrent} mA is dated {sentAt:O}, " +
                    "which is in the future - treating it as sent now");
                sentAt = now;
            }

            desiredCurrentSentAt = sentAt;

            var age = now - sentAt;
            if (age > SetpointFreshDuration)
            {
                // A replay of a setpoint that is past its freshness. Keep it as the best value
                // known, but neither write it to the box nor re-arm the release: the decision
                // is left to EnforceDesiredState, which does nothing while the setpoint is
                // merely stale and releases the box once it is older than StaleReleaseAfter.
                Console.WriteLine($"Charging command of {newCurrent} mA was sent " +
                    $"{age.TotalMinutes:F0} minutes ago: keeping the setpoint, but not applying it");
                return;
            }

            staleReleaseDone = false;

            if (!WritesEnabled())
            {
                Console.WriteLine("Environment variable KEBA_WRITE_TO_DEVICE is not set to 'true', so we will not write to the device");
                Console.WriteLine($"Would have written {newCurrent} mA as charging current to device otherwise");
                return;
            }
            if (newCurrent == lastWrittenCurrent)
            {
                // Already written; if the wallbox lost the value (e.g. session ended),
                // EnforceDesiredState detects the deviation from the device data and re-applies it.
                return;
            }
            WriteChargingCurrentToDevice(newCurrent);
            return;
        }

        /// <summary>
        /// Reconciles the device with the last desired current from the ChargingController.
        /// The wallbox falls back to its default (full) current when a charging session ends,
        /// so a fresh setpoint is re-applied whenever the device deviates. When the controller
        /// has been silent for too long, the box is released to full current once, so charging
        /// stays possible without the control loop (autonomous fallback).
        /// </summary>
        public async Task EnforceDesiredState(KebaData data, string deviceName)
        {
            if (!WritesEnabled())
                return;

            var now = time.GetUtcNow();
            var setpointAge = now - desiredCurrentSentAt;

            if (desiredCurrent >= 0 && setpointAge <= SetpointFreshDuration)
            {
                // A setpoint of 0 shows up as "Enable sys" = 0 on the box while "Curr user" keeps its old value
                var inSync = desiredCurrent == 0
                    ? !data.ChargingEnabled
                    : data.TargetCurrency == desiredCurrent;
                // Only relevant while a vehicle is connected; on plug-in the next read cycle corrects the box
                var vehicleConnected = data.PlugStatus == PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked;
                if (!inSync && vehicleConnected && now - lastEnforcementWriteAt >= TimeSpan.FromSeconds(10))
                {
                    Console.WriteLine($"Keba {deviceName}: device deviates from desired {desiredCurrent} mA " +
                        $"(Curr user={data.TargetCurrency} mA, charging enabled={data.ChargingEnabled}), re-applying");
                    lastEnforcementWriteAt = now;
                    WriteChargingCurrentToDevice(desiredCurrent);
                }
            }
            else if (setpointAge >= StaleReleaseAfter && !staleReleaseDone)
            {
                staleReleaseDone = true;
                var restricted = data.TargetCurrency != ReleaseCurrent || !data.ChargingEnabled;
                if (restricted)
                {
                    Console.WriteLine($"Keba {deviceName}: no charging command received for " +
                        $"{setpointAge.TotalMinutes:F0} minutes, releasing device to full current so charging stays possible");
                    WriteChargingCurrentToDevice(ReleaseCurrent);
                }
            }
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
                    data.State,
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

        /// <summary>
        /// Id of the charging session currently running in the box, null when none is running.
        /// </summary>
        public int? RunningSessionId { get; private set; }

        /// <summary>Start of the running session, null when none is running.</summary>
        public DateTimeOffset? SessionStart { get; private set; }

        /// <summary>
        /// Whether <see cref="SessionStart"/> had to be taken from the clock of the box instead
        /// of an observed plug-in edge.
        /// </summary>
        public bool SessionStartFromBoxClock { get; private set; }

        /// <summary>
        /// Follows the charging session of the box: which one is running, and since when.
        /// Call once per read cycle with the data of that cycle.
        /// </summary>
        /// <remarks>
        /// The start time comes from the plug-in edge this connector observes itself, because
        /// the recorded reports show "timeQ": 0 — the clock of the box is not synchronised and
        /// its timestamps may be arbitrarily wrong. Only a session that was already running
        /// when the connector started has no observed edge; there the box clock is the
        /// fallback, and the payload says so via SitzungsBeginnAusBoxZeit.
        /// </remarks>
        public void TrackSession(KebaData data, string wallbox)
        {
            var vehicleConnected = data.PlugStatus == PlugStatus.CablePluggedInChargingStationAndVehicleButNotLocked
                || data.PlugStatus == PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked;

            if (vehicleConnected && vehicleWasConnected == false)
            {
                // Remembered rather than used right away: the box opens the session a moment
                // after the plug is locked, so the new session id usually appears a cycle later.
                plugInObservedAt = time.GetUtcNow();
                Console.WriteLine($"Keba {wallbox}: vehicle plugged in at {plugInObservedAt:O}");
            }
            if (!vehicleConnected)
            {
                plugInObservedAt = null;
            }
            vehicleWasConnected = vehicleConnected;

            var session = ReadRunningSession(wallbox);
            if (session is null)
            {
                if (RunningSessionId is not null)
                    Console.WriteLine($"Keba {wallbox}: charging session {RunningSessionId} ended");
                RunningSessionId = null;
                SessionStart = null;
                SessionStartFromBoxClock = false;
                return;
            }

            if (session.SessionId == RunningSessionId)
                return;

            RunningSessionId = session.SessionId;
            if (plugInObservedAt is not null)
            {
                SessionStart = plugInObservedAt;
                SessionStartFromBoxClock = false;
                // Consumed: a later session must not inherit this edge
                plugInObservedAt = null;
            }
            else
            {
                SessionStart = session.StartTime;
                SessionStartFromBoxClock = session.StartTime is not null;
            }
            Console.WriteLine($"Keba {wallbox}: charging session {RunningSessionId} started at " +
                $"{SessionStart:O}{(SessionStartFromBoxClock ? " (from the unsynchronised box clock)" : "")}");
        }

        /// <summary>
        /// The session in report 100 — the current one — or null when it has already ended.
        /// </summary>
        protected virtual ChargingSession? ReadRunningSession(string wallbox)
        {
            var session = ReadReport(100, wallbox);
            if (session is null || session.SessionId == 0 || session.EndTime is not null)
                return null;
            return session;
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

        protected virtual void WriteChargingCurrentToDevice(int current)
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
                        lastWrittenCurrent = current;
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
                // Rethrow instead of returning an empty object: zeroed data must not be published
                // as real values or feed the desired-state reconciliation
                Console.WriteLine($"Error while reading device status: {ex.Message}\n {dataString}");
                throw;
            }
            if (data is null)
                throw new InvalidOperationException("Could not parse device status reports");
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
                    udpClient.Client.ReceiveTimeout = 5000; // 5 second timeout to prevent indefinite blocking
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