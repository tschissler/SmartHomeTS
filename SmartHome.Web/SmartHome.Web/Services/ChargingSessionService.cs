using System.Globalization;
using InfluxConnector;
using SharedContracts;

namespace SmartHome.Web.Services;

public interface IChargingSessionService
{
    Task<List<ChargingSession>> GetChargingSessionsAsync(DateTime start, DateTime end);
}

public class ChargingSessionService(IInfluxConnector influxConnector) : IChargingSessionService
{
    public async Task<List<ChargingSession>> GetChargingSessionsAsync(DateTime start, DateTime end)
    {
        // Replace with actual logic to execute the Flux query and fetch data
        // var query = """
        //                 // Query to get charging sessions with duplicates removed (keeping oldest record)
        //                 from(bucket: ""Smarthome_ChargingData"")
        //                   |> range(start: {start:yyyy-MM-dd}, stop: {end:yyyy-MM-dd})
        //                   |> filter(fn: (r) => 
        //                     (r._measurement == ""KebaGarage_ChargingSessionEnded"" or 
        //                      r._measurement == ""KebaOutside_ChargingSessionEnded"") and
        //                     (r._field == ""EnergyOfChargingSession"" or r._field == ""SessionID"")
        //                   )
        //                   |> pivot(
        //                     rowKey: [""_time"", ""_measurement""],
        //                     columnKey: [""_field""],
        //                     valueColumn: ""_value""
        //                   )
        //                   |> filter(fn: (r) => r.SessionID != 0 and r.EnergyOfChargingSession != 0)
        //                   |> map(fn: (r) => ({
        //                     endTime: r._time,
        //                     location: if r._measurement == ""KebaGarage_ChargingSessionEnded"" then ""Garage"" else ""Outside"",
        //                     sessionID: r.SessionID,
        //                     energyCharged: r.EnergyOfChargingSession
        //                   }))
        //                   |> group(columns: [""sessionID""])
        //                   |> sort(columns: [""endTime""], desc: false)
        //                   |> limit(n: 1)
        //                   |> group()
        //                   |> sort(columns: [""endTime""], desc: true)
        //                   |> yield(name: ""ChargingSessions"")";
        //              """;

        var query = """
                    from(bucket: "Smarthome_ChargingData")
                    |> range(start: -1mo)
                    |> filter(fn: (r) => r["_measurement"] == "ChargingSessionEnded")
                    |> pivot(rowKey: ["_time"], columnKey: ["_field"], valueColumn: "_value")
                    |> group(columns: ["SessionId"])
                    |> sort(columns: ["_time"], desc: true)
                    |> limit(n: 1)
                    |> group()
                    """;
        var tables = await influxConnector.QueryDataAsync(query);
        if (tables is not null && tables.Count() == 1)
        {
            return tables[0].Records.Select(r =>
            {
                string format = "MM/dd/yyyy HH:mm:ss zzz"; 
                DateTimeOffset.TryParseExact(r.GetValueByKey("EndTime").ToString(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime);
                DateTimeOffset.TryParseExact(r.GetValueByKey("StartTime").ToString(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime);
                Int32.TryParse(r.GetValueByKey("SessionId").ToString(), out var sessionId);
                return new ChargingSession
                (
                    SessionId: sessionId,
                    StartTime: startTime == DateTimeOffset.MinValue ? null : startTime,
                    EndTime: endTime == DateTimeOffset.MinValue ? null : endTime,
                    TatalEnergyAtStart: (double)r.GetValueByKey("TatalEnergyAtStart"),
                    EnergyOfChargingSession: (double)r.GetValueByKey("EnergyOfChargingSession"),
                    WallboxName: r.GetValueByKey("Wallbox")?.ToString() ?? ""
                );
            }).ToList();
        }
        return new List<ChargingSession>();
    }
}