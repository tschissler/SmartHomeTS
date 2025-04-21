using InfluxConnector;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using SharedContracts;
using SmartHomeHelpers.Logging;
using System.Drawing;
using System.Text.Json;

// Update these with your InfluxDB connection details.
const string influxUrl = "http://smarthomepi2:32086";
const string org = "smarthome";
string bucket = "Smarthome_ChargingData";

string? influxToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
if (String.IsNullOrEmpty(influxToken))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable INFLUX_TOKEN not set. Please set it to your InfluxDB token.");
    return;
}

string? envBucket = Environment.GetEnvironmentVariable("INFLUX_CHARINGDATA_BUCKET");
if (!String.IsNullOrEmpty(envBucket))
{
    bucket = envBucket;
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<MQTTClient.MQTTClient>(_ => new MQTTClient.MQTTClient("InfluxImporter"));
        services.AddSingleton<InfluxDbConnector>(_ => new InfluxDbConnector(influxUrl, org, influxToken));
        services.AddLogging();
    })
    .Build();
using var serviceScope = host.Services.CreateScope();
var services = serviceScope.ServiceProvider;
var influxConnector = services.GetRequiredService<InfluxDbConnector>();

// Setup MQTT client options
ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();
await mqttClient.SubscribeToTopic("data/charging/KebaGarage_ChargingSessionEnded");

mqttClient.OnMessageReceived += async (sender, e) =>
{
    ConsoleHelpers.PrintInformation($"Message received: {e.Topic}");
    try
    {
        string topic = e.Topic;
        string payload = e.Payload;

        switch (topic)
        {
            case "data/charging/KebaGarage_ChargingSessionEnded":
                WriteChargingSessionEndedData(bucket, influxConnector, "Garage", payload);
                break;
            case "data/charging/KebaOutside_ChargingSessionEnded":
                WriteChargingSessionEndedData(bucket, influxConnector, "Stellplatz", payload);
                break;
            default:
                var fields = new Dictionary<string, object>();
                var jsonData = JObject.Parse(payload);
                string measurementName = topic.Replace("data/charging/", "");

                foreach (var property in jsonData.Properties())
                {
                    var value = property.Value;
                    if (value.Type == JTokenType.Object)
                    {
                        foreach (var subProp in ((JObject)value).Properties())
                        {
                            var fieldName = $"{property.Name}_{subProp.Name}";
                            var subValue = subProp.Value;
                            fields.Add(fieldName, subValue);
                        }
                    }
                    else
                    {
                        fields.Add(property.Name, value);
                    }
                }

                influxConnector.WritePointDataToInfluxDb(
                    bucket,
                    measurementName,
                    fields,
                    new Dictionary<string, string>() { { "topic", topic } });
                break;
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.PrintErrorMessage($"####Error processing message: {ex.Message}");
    }
};

// Run the host to keep the application running and processing events
await host.RunAsync();

static void WriteChargingSessionEndedData(string bucket, InfluxDbConnector influxConnector, string wallbox, string payload)
{
    var fields = new Dictionary<string, object>();
    var data = JsonSerializer.Deserialize<ChargingSession>(payload);
    if (data != null)
    {
        if (!CheckIfSessionWithIdExists(influxConnector, bucket, data.SessionId, wallbox))
        {
            fields = new Dictionary<string, object>
                    {
                        { "SessionId", data.SessionId },
                        { "StartTime", data.StartTime },
                        { "EndTime", data.EndTime },
                        { "TatalEnergyAtStart", data.TatalEnergyAtStart },
                        { "EnergyOfChargingSession", data.EnergyOfChargingSession }
                    };
            influxConnector.WritePointDataToInfluxDb(
                bucket,
                "ChargingSessionEnded",
                fields,
                new Dictionary<string, string>() { { "Wallbox", wallbox } });
        }
    }
    else
    {
        ConsoleHelpers.PrintErrorMessage("Failed to deserialize ChargingSession data.");
    }
}

static bool CheckIfSessionWithIdExists(InfluxDbConnector influxConnector, string bucket, int sessionId, string wallbox)
{
    string query = $"""
            from(bucket: "{bucket}")
            |> range(start: -1mo)
            |> filter(fn: (r) => r["_measurement"] == "ChargingSessionEnded")
            |> filter(fn: (r) => r["Wallbox"] == "{wallbox}")
            |> filter(fn: (r) => r["_field"] == "SessionId")
            |> filter(fn: (r) => r["_value"] == {sessionId})
        """;
    var result = influxConnector.QueryDataAsync(query).Result;
    if (result != null && result.Count > 0 && result[0].Records.Count > 0)
    {
        ConsoleHelpers.PrintInformation($"Session with ID {sessionId} already exists in InfluxDB, ignoring new data.");
        return true;
    }
    return false;
}