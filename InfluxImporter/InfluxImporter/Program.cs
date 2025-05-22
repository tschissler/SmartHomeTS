using InfluxConnector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using SharedContracts;
using SmartHomeHelpers.Logging;
using System.Text.Json;

// Update these with your InfluxDB connection details.
const string influxUrl = "http://smarthomepi2:32086";

const string chargingBucket = "Smarthome_ChargingData";
const string electricityBucket = "Smarthome_ElectricityData";

string? influxToken = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN");
if (string.IsNullOrEmpty(influxToken))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable INFLUXDB_TOKEN not set. Please set it to your InfluxDB token.");
    return;
}

string? influxOrg = Environment.GetEnvironmentVariable("INFLUXDB_ORG");
if (string.IsNullOrEmpty(influxOrg))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable INFLUXDB_ORG not set. Please set it to your Influx organization.");
    return;
}

ConsoleHelpers.PrintInformation($"Using org: {influxOrg} at {influxUrl}");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<MQTTClient.MQTTClient>(_ => new MQTTClient.MQTTClient("InfluxImporter"));
        services.AddSingleton<InfluxDbConnector>(_ => new InfluxDbConnector(influxUrl, influxOrg, influxToken));
        services.AddLogging();
    })
    .Build();

using var serviceScope = host.Services.CreateScope();
var services = serviceScope.ServiceProvider;
var influxConnector = services.GetRequiredService<InfluxDbConnector>();

// Setup MQTT client options
ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();
await mqttClient.SubscribeToTopic("data/charging/#");
await mqttClient.SubscribeToTopic("data/electricity/M1/#");
await mqttClient.SubscribeToTopic("data/electricity/M3/#");
await mqttClient.SubscribeToTopic("data/electricity/envoym1");
await mqttClient.SubscribeToTopic("data/electricity/envoym3");


mqttClient.OnMessageReceived += async (sender, e) =>
{
    Dictionary<string, string> tags;
    //ConsoleHelpers.PrintInformation($"Message received: {e.Topic}");
    try
    {
        string topic = e.Topic;
        string payload = e.Payload;

        if (topic == "data/charging/KebaGarage_ChargingSessionEnded")
        {
            WriteChargingSessionEndedData(chargingBucket, influxConnector, "Garage", payload);
            return;
        }
        if (topic == "data/charging/KebaOutside_ChargingSessionEnded")
        {
            WriteChargingSessionEndedData(chargingBucket, influxConnector, "Stellplatz", payload);
            return;
        }
        if (topic.StartsWith("data/charging"))
        {
            WriteJsonPropertiesAsFields(chargingBucket, influxConnector, topic, topic.Replace("data/charging/", ""), payload, new Dictionary<string, string>());
            return;
        }
        if (topic == "data/electricity/envoym1")
        {
            tags = new Dictionary<string, string>();
            tags.Add("location", "M1");
            tags.Add("device", "EnvoyM1");
            WriteJsonPropertiesAsFields(electricityBucket, influxConnector, topic, "EnvoyM1", payload, tags, true);
        }
        if (topic == "data/electricity/envoym3")
        {
            tags = new Dictionary<string, string>();
            tags.Add("location", "M3");
            tags.Add("device", "EnvoyM3");
            WriteJsonPropertiesAsFields(electricityBucket, influxConnector, topic, "EnvoyM3", payload, tags, true);
            return;
        }
        if (topic.StartsWith("data/electricity/M1")
            || topic.StartsWith("data/electricity/M3"))
        {
            tags = new Dictionary<string, string>();
            var topicParts = topic.Split('/');
            tags.Add("location", topicParts[2]);
            tags.Add("group", topicParts[3]);
            tags.Add("device", topicParts[4]);
            WriteJsonPropertiesAsFields(electricityBucket, influxConnector, topic, topicParts[4], payload, tags, true);
            return;
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

    try
    {
        var result = influxConnector.QueryDataAsync(query).Result;
        if (result != null && result.Count > 0 && result[0].Records.Count > 0)
        {
            ConsoleHelpers.PrintInformation($"Session with ID {sessionId} already exists in InfluxDB, ignoring new data.");
            return true;
        }
    }
    catch(Exception _)
    { }
    return false;
}

static void WriteJsonPropertiesAsFields(string chargingBucket, InfluxDbConnector influxConnector, string topic, string measurementName, string payload, Dictionary<string, string> tags, bool enforceFloatValues = false)
{
    var fields = new Dictionary<string, object>();
    var jsonData = JObject.Parse(payload);

    foreach (var property in jsonData.Properties())
    {
        var value = property.Value;
        if (value.Type == JTokenType.Object)
        {
            foreach (var subProp in ((JObject)value).Properties())
            {
                var fieldName = $"{property.Name}_{subProp.Name}";
                var subValue = subProp.Value;
                if (enforceFloatValues && subValue.Type == JTokenType.Integer)
                {
                    fields.Add(fieldName, Convert.ToDouble(subValue));
                    continue;
                }
                fields.Add(fieldName, subValue);
            }
        }
        else
        {
            if (enforceFloatValues && value.Type == JTokenType.Integer)
            {
                fields.Add(property.Name, Convert.ToDouble(value));
                continue;
            }
            fields.Add(property.Name, value);
        }
    }

    tags.Add("topic", topic);

    influxConnector.WritePointDataToInfluxDb(
        chargingBucket,
        measurementName,
        fields,
        tags);
}