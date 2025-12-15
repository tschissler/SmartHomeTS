using System.Globalization;
using SharedContracts;

namespace SmartHome.Web.Services;

//public interface IChargingSessionService
//{
//    Task<List<ChargingSession>> GetChargingSessionsAsync(DateTime start, DateTime end);
//    Task<Dictionary<string, bool>> GetConnectedCarsAtTimeAsync(DateTimeOffset timestamp);
//    Task<(DateTimeOffset? EarliestStartTime, DateTimeOffset? LatestStartTime)> GetChargingSessionsTimeRange();
//}

//public class ChargingSessionService(IInfluxDBConnector influxConnector) : IChargingSessionService
//{
//    public async Task<List<ChargingSession>> GetChargingSessionsAsync(DateTime start, DateTime end)
//    {
//        var query = """
//                    from(bucket: "Smarthome_ChargingData")
//                    |> range(start: -1mo)
//                    |> filter(fn: (r) => r["_measurement"] == "ChargingSessionEnded")
//                    |> pivot(rowKey: ["_time"], columnKey: ["_field"], valueColumn: "_value")
//                    |> group(columns: ["SessionId"])
//                    |> sort(columns: ["_time"], desc: true)
//                    |> limit(n: 1)
//                    |> group()
//                    """;
//        var tables = await influxConnector.QueryDataAsync(query);
//        if (tables is not null && tables.Count() == 1)
//        {
//            var chargingSessions = new List<ChargingSession>();

//            foreach (var record in tables[0].Records)
//            {
//                DateTimeOffset.TryParse(record.GetValueByKey("EndTime").ToString(), out var endTime);
//                DateTimeOffset.TryParse(record.GetValueByKey("StartTime").ToString(), out var startTime);
//                Int32.TryParse(record.GetValueByKey("SessionId").ToString(), out var sessionId);
//                string wallboxName = record.GetValueByKey("Wallbox")?.ToString() ?? "";

//                // If we have a valid start time, check for connected cars
//                string? chargedCar = null;
//                if (startTime > DateTimeOffset.MinValue)
//                {
//                    var x = await GetConnectedCarsAtTimeAsync(startTime);
//                    if (x != null)
//                    {
//                        if (wallboxName == "Garage" && x.ContainsKey("BMW") && x["BMW"])
//                            chargedCar = "BMW";
//                        else if (wallboxName == "Stellplatz" && x.ContainsKey("VW") && x["VW"])
//                            chargedCar = "VW";
//                        else if (wallboxName == "Stellplatz" && x.ContainsKey("Mini") && x["Mini"])
//                            chargedCar = "Mini";
//                    }
//                }

//                var session = new ChargingSession
//                (
//                    SessionId: sessionId,
//                    StartTime: startTime == DateTimeOffset.MinValue ? null : startTime,
//                    EndTime: endTime == DateTimeOffset.MinValue ? null : endTime,
//                    TatalEnergyAtStart: (double)record.GetValueByKey("TatalEnergyAtStart") / 1000,
//                    EnergyOfChargingSession: (double)record.GetValueByKey("EnergyOfChargingSession") / 1000,
//                    WallboxName: wallboxName,
//                    ChargedCar: chargedCar
//                );

//                chargingSessions.Add(session);
//            }

//            return chargingSessions;
//        }
//        return new List<ChargingSession>();
//    }

//    public async Task<Dictionary<string, bool>> GetConnectedCarsAtTimeAsync(DateTimeOffset timestamp)
//    {
//        // Format the timestamp as ISO 8601 for the Flux query
//        string formattedStartTime = timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ");
//        string formattedStopTime = timestamp.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ssZ");

//        // Query to find car statuses near the specified time
//        var query = $"""
//                    from(bucket: "Smarthome_ChargingData")
//                    |> range(start: {formattedStartTime}, stop: {formattedStopTime})
//                    |> filter(fn: (r) => r["_measurement"] == "VW" or r["_measurement"] == "Mini" or r["_measurement"] == "BMW")
//                    |> filter(fn: (r) => r["_field"] == "chargerConnected")
//                    |> last()
//                    |> group(columns: ["_measurement"])
//                    |> pivot(rowKey: ["_measurement"], columnKey: ["_field"], valueColumn: "_value")
//                    |> yield(name: "combined")
//                    """;

//        var tables = await influxConnector.QueryDataAsync(query);

//        // Initialize result with default values (all cars disconnected)
//        var result = new Dictionary<string, bool>
//        {
//            { "BMW", false },
//            { "Mini", false },
//            { "VW", false }
//        };

//        // Update with actual values if found
//        if (tables != null && tables.Count() > 0 && tables[0].Records.Count > 0)
//        {
//            // Check each car brand and update the connection status
//            foreach (var table in tables)
//            {
//                var record = table.Records[0];
//                if ((bool)record.GetValueByKey("chargerConnected") == true)
//                {
//                    if (result.ContainsKey(record.GetValueByKey("_measurement").ToString()))
//                        result[record.GetValueByKey("_measurement").ToString()] = true;
//                }
//            }
//        }

//        return result;
//    }

//    public async Task<(DateTimeOffset? EarliestStartTime, DateTimeOffset? LatestStartTime)> GetChargingSessionsTimeRange()
//    {
//        var query = """
//                    from(bucket: "Smarthome_ChargingData")
//                    |> range(start: 2020-01-01T00:00:00Z)
//                    |> filter(fn: (r) => r["_measurement"] == "ChargingSessionEnded")
//                    |> filter(fn: (r) => r["_field"] == "StartTime")
//                    |> keep(columns: ["_time", "_value"])
//                    |> group()
//                    |> reduce(
//                        identity: {earliest: "2100-01-01T00:00:00Z", latest: "1970-01-01T00:00:00Z"},
//                        fn: (r, accumulator) => ({
//                          earliest: if r._value < accumulator.earliest then r._value else accumulator.earliest,
//                          latest: if r._value > accumulator.latest then r._value else accumulator.latest
//                        })
//                      )
//                    """;

//        var tables = await influxConnector.QueryDataAsync(query);

//        if (tables != null && tables.Count() > 0 && tables[0].Records.Count > 0)
//        {
//            var record = tables[0].Records[0];
//            DateTimeOffset? earliest = DateTimeOffset.TryParse(record.GetValueByKey("earliest").ToString(), out var tryearliest) ? tryearliest : null;
//            DateTimeOffset? latest = DateTimeOffset.TryParse(record.GetValueByKey("latest").ToString(), out var trylatest) ? trylatest : null;

//            return (earliest, latest);
//        }
//        return (null, null);
//    }
//}