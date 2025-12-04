using Google.Protobuf.WellKnownTypes;
using Influx3Connector;
using InfluxConnector;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using SharedContracts;
using SmartHome.DataHub;
using SmartHomeHelpers.Logging;
using System.Globalization;
using System.Text.Json;
using static Google.Protobuf.Reflection.SourceCodeInfo.Types;

const string influxUrl = "http://smarthomepi2:32086";
const string influx3Url = "http://smarthomepi2:32087";

const string chargingBucket = "Smarthome_ChargingData";
const string electricityBucket = "Smarthome_ElectricityData";
const string environmentDataBucket = "Smarthome_EnvironmentData";
const string heatingDataBucket = "Smarthome_HeatingData";

GeoPosition positionChargingStationStellplatz = new GeoPosition(Latitude: 48.412758, Longitude: 9.875185);
GeoPosition positionChargingStationGarage = new GeoPosition(Latitude: 48.412750277777775, Longitude: 9.875374444444445);
GeoPosition? bmwPosition = null;
GeoPosition? miniPosition = null;
GeoPosition? vwPosition = null;
string[] cars = new string[] { "BMW", "Mini", "VW" };
Dictionary<string, decimal> previousValues = new ();

string? influxToken = Environment.GetEnvironmentVariable("INFLUXDB_TOKEN");
if (string.IsNullOrEmpty(influxToken))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable INFLUXDB_TOKEN not set. Please set it to your InfluxDB token.");
    return;
}

string? influx3Token = Environment.GetEnvironmentVariable("SMARTHOME__INFLUXDB3_TOKEN");
if (string.IsNullOrEmpty(influx3Token))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable SMARTHOME__INFLUXDB3_TOKEN not set. Please set it to your InfluxDB token.");
    return;
}

string? dataHubEnvironment = Environment.GetEnvironmentVariable("SMARTHOME__DATAHUB_ENVIRONMENT");
if (string.IsNullOrEmpty(dataHubEnvironment))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable SMARTHOME__DATAHUB_ENVIRONMENT not set. Please set it to the environment you are running (Prod, Dev).");
    return;
}

string? influxOrg = Environment.GetEnvironmentVariable("INFLUXDB_ORG");
if (string.IsNullOrEmpty(influxOrg))
{
    ConsoleHelpers.PrintErrorMessage("Environmentvariable INFLUXDB_ORG not set. Please set it to your Influx organization.");
    return;
}

string influx3Database = "Smarthome_" + dataHubEnvironment;
ConsoleHelpers.PrintInformation($"Using org: {influxOrg} at {influxUrl}");
ConsoleHelpers.PrintInformation($"Using InfluxDB 3 at {influx3Url} with database {influx3Database}");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MQTTClient.MQTTClient>(_ => new MQTTClient.MQTTClient("InfluxImporter_" + dataHubEnvironment)); 
builder.Services.AddSingleton<InfluxDbConnector>(_ => new InfluxDbConnector(influxUrl, influxOrg, influxToken)); 
builder.Services.AddSingleton<Converters>(); 
builder.Services.AddSignalR(); 
builder.Services.AddLogging();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var mqttClient = scope.ServiceProvider.GetRequiredService<MQTTClient.MQTTClient>();
    await mqttClient.SubscribeToTopic("data/charging/#");
    await mqttClient.SubscribeToTopic("data/electricity/M1/#");
    await mqttClient.SubscribeToTopic("data/electricity/M3/#");
    await mqttClient.SubscribeToTopic("data/electricity/envoym1");
    await mqttClient.SubscribeToTopic("data/electricity/envoym3");
    await mqttClient.SubscribeToTopic("daten/temperatur/#");
    await mqttClient.SubscribeToTopic("daten/luftfeuchtigkeit/#");
    await mqttClient.SubscribeToTopic("data/thermostat/M1/shelly/#");
    await mqttClient.SubscribeToTopic("data/thermostat/M3/shelly/#");
    await mqttClient.SubscribeToTopic("cangateway/#");

    var influxConnector = scope.ServiceProvider.GetRequiredService<InfluxDbConnector>();

    var influx3Connector = new InfluxDB3Connector(influx3Url, influx3Database, influx3Token);
    var converters = scope.ServiceProvider.GetRequiredService<Converters>();

    // Setup MQTT client options
    ConsoleHelpers.PrintInformation(" ### Subscribing to MQTT topics");
    mqttClient.OnMessageReceived += async (sender, e) =>
    {
        Dictionary<string, string> tags;
        //ConsoleHelpers.PrintInformation($"Message received: {e.Topic}");

        string topic = e.Topic;
        string payload = e.Payload;

        try
        {
            if (topic == "data/charging/KebaGarage_ChargingSessionEnded")
            {
                var car = cars[positionChargingStationGarage.GetClosestNearby([bmwPosition, miniPosition, vwPosition]) ?? 0];
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

                WriteEnphaseDataToDB(influx3Connector, payload, "M1", "EnvoyM1");
                return;
            }
            if (topic == "data/electricity/envoym3")
            {
                tags = new Dictionary<string, string>();
                tags.Add("location", "M3");
                tags.Add("device", "EnvoyM3");
                WriteJsonPropertiesAsFields(electricityBucket, influxConnector, topic, "EnvoyM3", payload, tags, true);

                WriteEnphaseDataToDB(influx3Connector, payload, "M3", "EnvoyM3");
                return;
            }
            if (topic.StartsWith("data/electricity/M1")
                || topic.StartsWith("data/electricity/M3"))
            {
                tags = new Dictionary<string, string>();
                var topicParts = topic.Split('/');
                var location = topicParts[2];
                var device = topicParts[4];
                tags.Add("location", location);
                tags.Add("group", topicParts[3]);
                tags.Add("device", device);
                WriteJsonPropertiesAsFields(electricityBucket, influxConnector, topic, device, payload, tags, true);

                if (topicParts[3] == "Smartmeter")
                {
                    try
                    {
                        var smartmeterData = JsonSerializer.Deserialize<SmartmeterData>(payload);
                        if (smartmeterData != null)
                        {
                            influx3Connector.WriteInfluxRecords(converters.SmartmeterDataToInfluxRecords(smartmeterData, location, device));
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelpers.PrintErrorMessage($"####Error deserializing Smartmeter data: {ex.Message}");
                    }
                }
                else if (topicParts[3] == "shelly")
                {
                    try
                    {
                        var shellyPowerData = JsonSerializer.Deserialize<ShellyPowerData>(payload);
                        if (shellyPowerData != null)
                        {
                            influx3Connector.WriteInfluxRecords(converters.ShellyPowerDataToInfluxRecords(shellyPowerData, location, device));
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelpers.PrintErrorMessage($"####Error deserializing Shelly data: {ex.Message}");
                    }
                }
                return;
            }

            if (topic.StartsWith("daten/temperatur/"))
            {
                var topicParts = topic.Split('/');
                var location = topicParts[2];
                var measurement = topicParts[3];
                var measurementId = "Temperatur_" + location + "_" + measurement;
                influx3Connector.WriteTemperatureValue(
                    new InfluxTemperatureRecord
                    {
                        MeasurementId = measurementId,
                        Category = MeasurementCategory.Temperature,
                        SubCategory = "-",
                        SensorType = "TempSensor",
                        Location = location,
                        Device = "TempSensor",
                        Measurement = measurement,
                        Value_DegreeC = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture),
                        MeasurementType = MeasurementType.Temperature
                    },
                    DateTimeOffset.UtcNow);
                return;
            }

            if (topic.StartsWith("daten/luftfeuchtigkeit/") && payload != "nan")
            {
                var topicParts = topic.Split('/');
                var location = topicParts[2];
                var measurement = topicParts[3];
                var measurementId = "Luftfeuchtigkeit_" + location + "_" + measurement;
                influx3Connector.WritePercentageValue(
                    new InfluxPercentageRecord
                    {
                        MeasurementId = measurementId,
                        SubCategory = "-",
                        Category = MeasurementCategory.Humidity,
                        SensorType = "TempSensor",
                        Location = location,
                        Device = "TempSensor",
                        Measurement = measurement,
                        Value_Percent = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture),
                        MeasurementType = MeasurementType.Temperature
                    },
                    DateTimeOffset.UtcNow);
                return;
            }

            //if (topic.StartsWith("data/thermostat/M1/shelly/") || topic.StartsWith("data/thermostat/M3/shelly/"))
            //{
            //    ShellyThermostatData? data;
            //    try
            //    {
            //        data = JsonSerializer.Deserialize<ShellyThermostatData>(payload);
            //    }
            //    catch (Exception ex)
            //    {
            //        ConsoleHelpers.PrintErrorMessage($"####Error deserializing ShellyThermostat data for topic {topic}: {ex.Message}");
            //        ConsoleHelpers.PrintErrorMessage($"Payload: {payload}");
            //        data = null;
            //    }

            //    if (data != null)
            //    {
            //        tags = new Dictionary<string, string>();
            //        var topicParts = topic.Split('/');
            //        var location = topicParts[2];
            //        var device = topicParts[4];
            //        var measurement = "CurrentTemperature";
            //        influx3Connector.WriteTemperatureValue(
            //            new InfluxTemperatureRecord
            //            {
            //                MeasurementId = measurementId,
            //                Category = MeasurementCategory.Temperature,
            //                SubCategory = "Thermostat",
            //                SensorType = "Thermostat",
            //                Location = location,
            //                Device = "Shelly",
            //                Measurement = measurement,
            //                Value_DegreeC = data.CurrentTemperature,
            //                MeasurementType = MeasurementType.Temperature
            //            },
            //            DateTimeOffset.UtcNow);
            //    }
            //    return;
            //}

            if (topic.StartsWith("cangateway"))
            {
                tags = new Dictionary<string, string>();
                var topicParts = topic.Split('/');
                WriteCangatewayDataToDB(influx3Connector, payload, topicParts);
                return;
            }
        }
        catch (Exception ex)
        {
            ConsoleHelpers.PrintErrorMessage($"####Error processing message: {ex.Message}");
        }
    };
}

app.Run();

void WriteCangatewayDataToDB(InfluxDB3Connector influx3Connector, string payload, string[] topicParts)
{
    if (topicParts.Length < 4)
        return;
    var location = topicParts[1];
    var subCategory = topicParts[2];
    var measurementType = topicParts[3];
    var meassurement = topicParts[4];

    var measurementId = "Heizung_" + location + "_" + subCategory + "_" + meassurement;

    switch (measurementType)
    {
        case "Temperatur":
            influx3Connector.WriteTemperatureValue(
                new InfluxTemperatureRecord
                {
                    MeasurementId = measurementId,
                    Category = MeasurementCategory.Heizung,
                    SubCategory = subCategory,
                    SensorType = "CanGateway",
                    Location = location,
                    Device = "Waermepumpe",
                    Measurement = meassurement,
                    Value_DegreeC = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture),
                    MeasurementType = MeasurementType.Temperature
                },
                DateTimeOffset.UtcNow);
            break;
        case "Leistung":
            influx3Connector.WritePowerValue(
                new InfluxPowerRecord
                {
                    MeasurementId = measurementId,
                    Category = MeasurementCategory.Heizung,
                    SubCategory = subCategory,
                    SensorType = "CanGateway",
                    Location = location,
                    Device = "Waermepumpe",
                    Measurement = meassurement,
                    Value_W = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture),
                    MeasurementType = MeasurementType.Power
                },
                DateTimeOffset.UtcNow);
            break;
        case "Prozent":
            influx3Connector.WritePercentageValue(
                new InfluxPercentageRecord
                {
                    MeasurementId = measurementId,
                    Category = MeasurementCategory.Heizung,
                    SubCategory = subCategory,
                    SensorType = "CanGateway",
                    Location = location,
                    Device = "Waermepumpe",
                    Measurement = meassurement,
                    Value_Percent = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture),
                    MeasurementType = MeasurementType.Percent
                },
                DateTimeOffset.UtcNow);
            break;
        case "Status":
            influx3Connector.WriteStatusValue(
                new InfluxStatusRecord
                {
                    MeasurementId = measurementId,
                    Category = MeasurementCategory.Heizung,
                    SubCategory = subCategory,
                    SensorType = "CanGateway",
                    Location = location,
                    Device = "Waermepumpe",
                    Measurement = meassurement,
                    Value_Status = Int16.Parse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    MeasurementType = MeasurementType.Status
                },
                DateTimeOffset.UtcNow);
            break;
        case "Energie":
            var currentValue = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture);
            var delta = 0m;
            var previousValue = previousValues.FirstOrDefault(kv => kv.Key == measurementId);
            if (previousValue.Key != null)
            {
                delta = currentValue - previousValue.Value;
                previousValues[measurementId] = currentValue;
            }
            else
            {
                previousValues.Add(measurementId, currentValue);
            }
            influx3Connector.WriteEnergyValue(
                new InfluxEnergyRecord
                {
                    MeasurementId = measurementId,
                    Category = MeasurementCategory.Heizung,
                    SubCategory = MeasurementSubCategory.Consumption,
                    SensorType = "CanGateway",
                    Location = location,
                    Device = "Waermepumpe",
                    Measurement = meassurement,
                    Value_Cumulated_KWh = currentValue,
                    Value_Delta_KWh = delta,
                    MeasurementType = MeasurementType.Energy
                },
                DateTimeOffset.UtcNow);
            break;
        case "Zaehler":
            influx3Connector.WriteCounterValue(
                new InfluxCounterRecord
                {
                    MeasurementId = measurementId,
                    Category = MeasurementCategory.Heizung,
                    SubCategory = subCategory,
                    SensorType = "CanGateway",
                    Location = location,
                    Device = "Waermepumpe",
                    Measurement = meassurement,
                    Value_Counter = Int16.Parse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    MeasurementType = MeasurementType.Counter
                },
                DateTimeOffset.UtcNow);
            break;
    }
}

void WriteEnphaseDataToDB(InfluxDB3Connector influx3Connector, string payload, string location, string device)
{
    try
    {
        var enphaseData = JsonSerializer.Deserialize<EnphaseData>(payload);
        if (enphaseData != null)
        {
            var converters = app.Services.GetRequiredService<Converters>();
            influx3Connector.WriteInfluxRecords(converters.EnphaseDataToInfluxRecords(enphaseData, location, device));
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.PrintErrorMessage($"####Error deserializing Enphase data: {ex.Message}");
    }
}

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