using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SmartHomeHelpers.Logging;

// Update these with your InfluxDB connection details.
const string InfluxUrl = "http://smarthomepi2:32086";
const string Org = "smarthome";
const string Bucket = "Smarthome_ChargingData";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<MQTTClient.MQTTClient>(provider => new MQTTClient.MQTTClient("InfluxImporter"));
        services.AddLogging();
    })
    .Build();
using var serviceScope = host.Services.CreateScope();
var services = serviceScope.ServiceProvider;

string? InfluxToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
if (String.IsNullOrEmpty(InfluxToken))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable INFLUX_TOKEN not set. Please set it to your InfluxDB token.");
    return;
}

// Create InfluxDB client
var options = InfluxDBClientOptions.Builder
               .CreateNew()
               .Url(InfluxUrl)
               .AuthenticateToken(InfluxToken.ToCharArray())
               .LogLevel(InfluxDB.Client.Core.LogLevel.None)
               .Build();
using var influxDBClient = InfluxDBClientFactory.Create(options);
var writeApi = influxDBClient.GetWriteApi();

// Setup MQTT client options
ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();
await mqttClient.SubscribeToTopic("data/charging/#");

mqttClient.OnMessageReceived += (sender, e) =>
{
    ConsoleHelpers.PrintInformation($"Message received: {e.Topic}");
    try
    {
        string topic = e.Topic;
        string payload = e.Payload;

        // Parse the JSON payload
        var jsonData = JObject.Parse(payload);

        // Use the topic (trimmed) as the measurement name
        string measurementName = topic.Replace("data/charging/", "");

        // Build the InfluxDB point
        var point = PointData.Measurement(measurementName)
            .Tag("topic", topic)
            .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        // Add fields from the JSON
        foreach (var property in jsonData.Properties())
        {
            var value = property.Value;
            // Store booleans, numbers, or strings accordingly
            if (value.Type == JTokenType.Boolean)
            {
                point = point.Field(property.Name, value.Value<bool>());
            }
            else if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                point = point.Field(property.Name, value.Value<double>());
            }
            else if (value.Type == JTokenType.String)
            {
                point = point.Field(property.Name, value.Value<string>());
            }
            // Handle nested JSON objects (e.g. position)
            else if (value.Type == JTokenType.Object)
            {
                foreach (var subProp in ((JObject)value).Properties())
                {
                    var fieldName = $"{property.Name}_{subProp.Name}";
                    var subValue = subProp.Value;
                    if (subValue.Type == JTokenType.Integer || subValue.Type == JTokenType.Float)
                    {
                        point = point.Field(fieldName, subValue.Value<double>());
                    }
                    else if (subValue.Type == JTokenType.String)
                    {
                        point = point.Field(fieldName, subValue.Value<string>());
                    }
                }
            }
        }

        // Write the point to InfluxDB
        writeApi.WritePoint(point, Bucket, Org);
        ConsoleHelpers.PrintInformation($"Data written to InfluxDB bucket '{Bucket}'");
    }
    catch (Exception ex)
    {
        ConsoleHelpers.PrintErrorMessage($"####Error processing message: {ex.Message}");
    }
};

// Run the host to keep the application running and processing events
await host.RunAsync();