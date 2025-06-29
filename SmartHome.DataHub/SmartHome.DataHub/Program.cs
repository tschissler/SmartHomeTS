﻿using InfluxConnector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using SharedContracts;
using SmartHomeHelpers.Logging;
using System.Globalization;
using System.Text.Json;

const string influxUrl = "http://smarthomepi2:32086";

const string chargingBucket = "Smarthome_ChargingData";
const string electricityBucket = "Smarthome_ElectricityData";
const string environmentDataBucket = "Smarthome_EnvironmentData";

GeoPosition positionChargingStationStellplatz = new GeoPosition(Latitude: 48.412758, Longitude: 9.875185);
GeoPosition positionChargingStationGarage = new GeoPosition(Latitude: 48.412750277777775, Longitude: 9.875374444444445);
GeoPosition? bmwPosition = null;
GeoPosition? miniPosition = null;
GeoPosition? vwPosition = null;
string[] cars = new string[] { "BMW", "Mini", "VW" };

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
ConsoleHelpers.PrintInformation(" ### Subscribing to MQTT topics");

var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();
await mqttClient.SubscribeToTopic("data/charging/#");
await mqttClient.SubscribeToTopic("data/electricity/M1/#");
await mqttClient.SubscribeToTopic("data/electricity/M3/#");
await mqttClient.SubscribeToTopic("data/electricity/envoym1");
await mqttClient.SubscribeToTopic("data/electricity/envoym3");
await mqttClient.SubscribeToTopic("daten/temperatur/#");
await mqttClient.SubscribeToTopic("daten/luftfeuchtigkeit/#");


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
            var car = cars[positionChargingStationGarage.GetClosestNearby([bmwPosition, miniPosition, vwPosition])??0];
            var json = JObject.Parse(payload);
            json["car"] = car;
            WriteChargingSessionEndedData(chargingBucket, influxConnector, "Garage", json.ToString());
            return;
        }
        if (topic == "data/charging/KebaOutside_ChargingSessionEnded")
        {
            var car = cars[positionChargingStationStellplatz.GetClosestNearby([bmwPosition, miniPosition, vwPosition]) ?? 2];
            var json = JObject.Parse(payload);
            json["car"] = car;
            WriteChargingSessionEndedData(chargingBucket, influxConnector, "Stellplatz", json.ToString());
            return;
        }
        if (topic.StartsWith("data/charging/BMW"))
        {
            bmwPosition = ExtractPositionFromPayload(payload) ?? bmwPosition;
        }
        if (topic.StartsWith("data/charging/Mini"))
        {
            miniPosition = ExtractPositionFromPayload(payload) ?? miniPosition;
        }
        if (topic.StartsWith("data/charging/VW"))
        {
            vwPosition = ExtractPositionFromPayload(payload) ?? vwPosition;
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
            return;
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

        if (topic.StartsWith("daten/temperatur/") || topic.StartsWith("daten/luftfeuchtigkeit/"))
        {
            tags = new Dictionary<string, string>();
            var topicParts = topic.Split('/');
            tags.Add("device", topicParts[2]);
            WriteFloatAsFields(environmentDataBucket, influxConnector, topic, topicParts[1], payload, tags);
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

static void WriteFloatAsFields(string chargingBucket, InfluxDbConnector influxConnector, string topic, string measurementName, string payload, Dictionary<string, string> tags)
{
    var fields = new Dictionary<string, object>
    {
        { "value", Convert.ToDouble(payload, CultureInfo.InvariantCulture) }
    };
 
    tags.Add("topic", topic);

    influxConnector.WritePointDataToInfluxDb(
        chargingBucket,
        measurementName,
        fields,
        tags);
}

static GeoPosition? ExtractPositionFromPayload(string payload)
{
    var json = JObject.Parse(payload);
    var position = json["position"];
    if (position != null)
    {
        var latitude = position.Value<double?>("latitude");
        var longitude = position.Value<double?>("longitude");
        if (latitude.HasValue && longitude.HasValue)
        {
            return new GeoPosition(longitude.Value, latitude.Value);
        }
        else
        {
            ConsoleHelpers.PrintErrorMessage("Latitude or longitude missing in position payload.");
        }
    }
    else
    {
        ConsoleHelpers.PrintErrorMessage("Position object missing in payload.");
    }

    return null;
}

public record GeoPosition(double Longitude, double Latitude)
{
    public int? GetClosestNearby(List<GeoPosition?> positions)
    {
        positions = positions.Where(p => p is GeoPosition).ToList();
        if (positions == null || positions.Count == 0)
            return null;

        GeoPosition? closest = null;
        double minDistance = double.MaxValue;

        foreach (var pos in positions)
        {
            double distance = GetDistanceInMeters(this.Latitude, this.Longitude, pos.Latitude, pos.Longitude);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = pos;
            }
        }

        return positions.IndexOf(closest!);
    }

    public static double GetDistanceInMeters(double lat1, double lon1, double lat2, double lon2)
    {
        // Convert to radians
        double dLat1 = lat1 * Math.PI / 180.0;
        double dLon1 = lon1 * Math.PI / 180.0;
        double dLat2 = lat2 * Math.PI / 180.0;
        double dLon2 = lon2 * Math.PI / 180.0;

        // Haversine formula
        double dLat = dLat2 - dLat1;
        double dLon = dLon2 - dLon1;
        double a = Math.Pow(Math.Sin(dLat / 2), 2) +
                   Math.Cos(dLat1) * Math.Cos(dLat2) *
                   Math.Pow(Math.Sin(dLon / 2), 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double earthRadius = 6371000; // meters
        return earthRadius * c;
    }
};