using Influx3Connector;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json.Linq;
using SharedContracts;
using SmartHome.DataHub;
using System.Globalization;
using System.Text.Json;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    PropertyNameCaseInsensitive = true
};

var builder = WebApplication.CreateBuilder(args);
var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});
var logger = loggerFactory.CreateLogger("Program");

// Display version information on startup
var versionInfo = VersionInfo.GetVersionInfo();
logger.LogInformation("╔════════════════════════════════════════════════════════════════════╗");
logger.LogInformation("║  SmartHome.DataHub Starting                                        ║");
logger.LogInformation("╠════════════════════════════════════════════════════════════════════╣");
logger.LogInformation($"║  {versionInfo.GetDisplayString().PadRight(66)}║");
logger.LogInformation("╚════════════════════════════════════════════════════════════════════╝");

GeoPosition positionChargingStationStellplatz = new GeoPosition(Latitude: 48.412758, Longitude: 9.875185);
GeoPosition positionChargingStationGarage = new GeoPosition(Latitude: 48.412750277777775, Longitude: 9.875374444444445);
GeoPosition? bmwPosition = null;
GeoPosition? miniPosition = null;
GeoPosition? vwPosition = null;
string[] cars = new string[] { "BMW", "Mini", "VW" };
Dictionary<string, decimal> previousValues = new();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

string? influx3Url = configuration["SMARTHOME__INFLUX3_URL"] ?? configuration["SMARTHOME:INFLUX3_URL"];
if (string.IsNullOrEmpty(influx3Url))
{
    logger.LogError("Environmentvariable SMARTHOME__INFLUX3_URL not set. Please set it to your InfluxDB 3 URL.");
    return;
}

string? influx3Token = configuration["SMARTHOME__INFLUXDB3_TOKEN"] ?? configuration["SMARTHOME:INFLUXDB3_TOKEN"];
if (string.IsNullOrEmpty(influx3Token))
{
    logger.LogError("Environmentvariable SMARTHOME__INFLUXDB3_TOKEN not set. Please set it to your InfluxDB token.");
    return;
}

string? dataHubEnvironment = configuration["SMARTHOME__DATAHUB_ENVIRONMENT"] ?? configuration["SMARTHOME:DATAHUB_ENVIRONMENT"];
if (string.IsNullOrEmpty(dataHubEnvironment))
{
    logger.LogError("Environmentvariable SMARTHOME__DATAHUB_ENVIRONMENT not set. Please set it to the environment you are running (Prod, Dev).");
    return;
}

string? influxOrg = configuration["SMARTHOME__INFLUXDB_ORG"] ?? configuration["SMARTHOME:INFLUXDB_ORG"];
if (string.IsNullOrEmpty(influxOrg))
{
    logger.LogError("Environmentvariable SMARTHOME__INFLUXDB_ORG not set. Please set it to your Influx organization.");
    return;
}

string? mqttBroker = configuration["SMARTHOME__MQTT_BROKER"] ?? configuration["SMARTHOME:MQTT_BROKER"];
if (string.IsNullOrEmpty(mqttBroker))
{
    logger.LogError("Environmentvariable SMARTHOME_MQTT_BROKER not set. Please set it to your MQTT broker hostname.");
    return;
}
var brokerParts = mqttBroker.Split(":");
if (brokerParts.Length != 2)
{
    logger.LogError("Environmentvariable SMARTHOME_MQTT_BROKER is not in the correct format. Please set it to your MQTT broker hostname:port.");
    return;
}
string mqttBrokerHost = brokerParts[0];
if (!int.TryParse(brokerParts[1], out int mqttBrokerPort))
{
    logger.LogError("Environmentvariable SMARTHOME_MQTT_BROKER port is not a valid integer. Please set it to your MQTT broker hostname:port.");
    return;
}

string influx3Database = "Smarthome_" + dataHubEnvironment;
//logger.LogInformation($"Using org: {influxOrg} at {influxUrl}");
logger.LogInformation($"Using InfluxDB 3 at {influx3Url} with database {influx3Database}");
logger.LogInformation($"Using MQTT broker at {mqttBrokerHost}:{mqttBrokerPort}");

builder.Services.AddSingleton<MQTTClient.MQTTClient>(_ => new MQTTClient.MQTTClient("InfluxImporter_" + dataHubEnvironment, mqttBrokerHost, mqttBrokerPort));
//builder.Services.AddSingleton<InfluxDbConnector>(_ => new InfluxDbConnector(influxUrl, influxOrg, influxToken));
builder.Services.AddSingleton<InfluxDB3Connector>(_ => new InfluxDB3Connector(influx3Url, influx3Database, influx3Token, logger));
builder.Services.AddSingleton<Converters>();
builder.Services.AddSignalR();
builder.Services.AddLogging();

// Register health checks
builder.Services.AddHealthChecks()
    .AddCheck<DataPipelineHealthCheck>("data-pipeline", tags: new[] { "live", "ready" });

var app = builder.Build();

app.MapGet("/version", () => 
{
    var version = VersionInfo.GetVersionInfo();
    return Results.Ok(new
    {
        service = "SmartHome.DataHub",
        version = version.Version,
        buildNumber = version.BuildNumber,
        buildDate = version.BuildDate,
        gitCommit = version.GitCommit
    });
});

app.MapGet("/health", (InfluxDB3Connector connector) => 
{
    if (connector.TimeSinceLastWrite > TimeSpan.FromMinutes(3))
        return Results.StatusCode(503); // Unhealthy
    return Results.Ok();
});

app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = (check) => check.Tags.Contains("live"),
});

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = (check) => check.Tags.Contains("ready"),
});

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
    await mqttClient.SubscribeToTopic("daten/Heizkörperlüfter/#");

    var influx3Connector = scope.ServiceProvider.GetRequiredService<InfluxDB3Connector>();
    var converters = scope.ServiceProvider.GetRequiredService<Converters>();

    // Setup MQTT client options
    logger.LogInformation(" ### Subscribing to MQTT topics");
    mqttClient.OnMessageReceived += async (sender, e) =>
    {
        Dictionary<string, string> tags;
        //logger.LogInformation($"Message received: {e.Topic}");

        string topic = e.Topic;
        string payload = e.Payload;
        var topicParts = topic.Split('/');

        try
        {
            if (topic.StartsWith("daten/Heizkörperlüfter/"))
            {
                var genericSensorData = JsonSerializer.Deserialize<GenericSensorJsonData>(payload, jsonOptions);
                if (genericSensorData != null)
                {
                    influx3Connector.WriteInfluxRecords(converters.GenericSensorJsonDataToInfluxRecords(genericSensorData));
                }
                return;
            }

            if (topic == "data/charging/KebaOutside" ||
                topic == "data/charging/KebaGarage")
            {
                ChargingGetData? chargingData;
                try
                {
                    chargingData = JsonSerializer.Deserialize<ChargingGetData>(payload, jsonOptions);
                }
                catch (Exception ex)
                {
                    logger.LogError($"####Error deserializing charging chargingData for topic {topic}: {ex.Message}");
                    logger.LogError($"Payload: {payload}");
                    chargingData = null;
                }
                if (chargingData != null)
                {
                    tags = new Dictionary<string, string>();
                    var location = "M3";
                    var device = topicParts[2];
                    var measurement = "AutoIstEingesteckt";
                    influx3Connector.WriteStatusValue(
                        new InfluxStatusRecord
                        {
                            MeasurementId = $"Status_{location}_{device}_{measurement}",
                            Category = MeasurementCategory.Laden,
                            SubCategory = "Verbindungsstatus",
                            SensorType = "Wallbox",
                            Location = location,
                            Device = device,
                            Measurement = measurement,
                            Value_Status = chargingData.CarIsPlugedIn ? 1m : 0m,
                        },
                        DateTimeOffset.UtcNow);
                    measurement = "AktuelleLadeleistung";
                    influx3Connector.WritePowerValue(
                        new InfluxPowerRecord
                        {
                            MeasurementId = $"Leistung_{location}_{device}_{measurement}",
                            Category = MeasurementCategory.Laden,
                            SubCategory = "Ladeleistung",
                            SensorType = "Wallbox",
                            Location = location,
                            Device = device,
                            Measurement = measurement,
                            Value_W = chargingData.CurrentChargingPower,
                        },
                        DateTimeOffset.UtcNow);

                    decimal currentValue = chargingData.EnergyCurrentChargingSession / 1000m;
                    measurement = "EnergieAktuelleLadesitzung";
                    var measurementId = $"Energie_{location}_{device}_{measurement}";
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
                            Category = MeasurementCategory.Laden,
                            SubCategory = MeasurementSubCategory.Consumption,
                            SensorType = "Wallbox",
                            Location = location,
                            Device = device,
                            Measurement = measurement,
                            Value_Cumulated_KWh = currentValue,
                            Value_Delta_KWh = delta,
                        },
                        DateTimeOffset.UtcNow);

                    currentValue = chargingData.EnergyTotal / 1000m;
                    measurement = "EnergieGesamt";
                    measurementId = $"Energie_{location}_{device}_{measurement}";
                    delta = 0m;
                    previousValue = previousValues.FirstOrDefault(kv => kv.Key == measurementId);
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
                            Category = MeasurementCategory.Laden,
                            SubCategory = MeasurementSubCategory.Consumption,
                            SensorType = "Wallbox",
                            Location = location,
                            Device = device,
                            Measurement = measurement,
                            Value_Cumulated_KWh = currentValue,
                            Value_Delta_KWh = delta,
                        },
                        DateTimeOffset.UtcNow);
                }
                return;
            }

            if (topic == "data/electricity/envoym1")
            {
                WriteEnphaseDataToDB(influx3Connector, payload, "M1", "EnvoyM1");
                return;
            }
            if (topic == "data/electricity/envoym3")
            {
                WriteEnphaseDataToDB(influx3Connector, payload, "M3", "EnvoyM3");
                return;
            }
            if (topic.StartsWith("data/electricity/M1")
                || topic.StartsWith("data/electricity/M3"))
            {
                var location = topicParts[2];
                var device = topicParts[4];

                if (topicParts[3] == "Smartmeter")
                {
                    try
                    {
                        var smartmeterData = JsonSerializer.Deserialize<SmartmeterData>(payload, jsonOptions);
                        if (smartmeterData != null)
                        {
                            influx3Connector.WriteInfluxRecords(converters.SmartmeterDataToInfluxRecords(smartmeterData, location, device));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"####Error deserializing Smartmeter data: {ex.Message}");
                    }
                }
                else if (topicParts[3] == "shelly")
                {
                    try
                    {
                        var shellyPowerData = JsonSerializer.Deserialize<ShellyPowerData>(payload, jsonOptions);
                        if (shellyPowerData != null)
                        {
                            influx3Connector.WriteInfluxRecords(converters.ShellyPowerDataToInfluxRecords(shellyPowerData, location, device));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"####Error deserializing Shelly data: {ex.Message}");
                    }
                }
                return;
            }

            if (topic.StartsWith("daten/temperatur/"))
            {
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
                    },
                    DateTimeOffset.UtcNow);
                return;
            }

            if (topic.StartsWith("daten/luftfeuchtigkeit/") && payload != "nan")
            {
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
                    },
                    DateTimeOffset.UtcNow);
                return;
            }

            if (topic.StartsWith("data/thermostat/M1/shelly/") || topic.StartsWith("data/thermostat/M3/shelly/"))
            {
                ShellyThermostatData? data;
                try
                {
                    data = JsonSerializer.Deserialize<ShellyThermostatData>(payload, jsonOptions);
                }
                catch (Exception ex)
                {
                    logger.LogError($"####Error deserializing ShellyThermostat data for topic {topic}: {ex.Message}");
                    logger.LogError($"Payload: {payload}");
                    data = null;
                }

                if (data != null)
                {
                    tags = new Dictionary<string, string>();
                    var location = topicParts[2];
                    var device = topicParts[4];
                    influx3Connector.WriteTemperatureValue(
                        new InfluxTemperatureRecord
                        {
                            MeasurementId = $"Temperatur_{location}_{device}_Isttemperatur",
                            Category = MeasurementCategory.Temperature,
                            SubCategory = "Ist",
                            SensorType = "Thermostat",
                            Location = location,
                            Device = device,
                            Measurement = "Isttemperatur",
                            Value_DegreeC = data.CurrentTemperature,
                        },
                        DateTimeOffset.UtcNow);
                    influx3Connector.WriteTemperatureValue(
                        new InfluxTemperatureRecord
                        {
                            MeasurementId = $"Temperatur_{location}_{device}_Solltemperatur",
                            Category = MeasurementCategory.Temperature,
                            SubCategory = "Soll",
                            SensorType = "Thermostat",
                            Location = location,
                            Device = device,
                            Measurement = "Solltemperatur",
                            Value_DegreeC = data.TargetTemperature,
                        },
                        DateTimeOffset.UtcNow);
                    influx3Connector.WritePercentageValue(
                        new InfluxPercentageRecord
                        {
                            MeasurementId = $"Ventilposition_{location}_{device}",
                            Category = MeasurementCategory.Ventil,
                            SubCategory = "Ist",
                            SensorType = "Thermostat",
                            Location = location,
                            Device = device,
                            Measurement = "Ventilposition",
                            Value_Percent = data.ValvePosition,
                        },
                        DateTimeOffset.UtcNow);
                    influx3Connector.WritePercentageValue(
                        new InfluxPercentageRecord
                        {
                            MeasurementId = $"Batterie_{location}_{device}",
                            Category = MeasurementCategory.Electricity,
                            SubCategory = "Ist",
                            SensorType = "Thermostat",
                            Location = location,
                            Device = device,
                            Measurement = "Batterie",
                            Value_Percent = data.BatteryLevel,
                        },
                        DateTimeOffset.UtcNow);
                }
                return;
            }

            if (topic.StartsWith("cangateway"))
            {
                tags = new Dictionary<string, string>();
                WriteCangatewayDataToDB(influx3Connector, payload, topicParts);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"####Error processing message: {ex.Message}");
            logger.LogError(topic);
            logger.LogError(payload);
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
                },
                DateTimeOffset.UtcNow);
            break;
        case "Energie":
            var currentValue = Decimal.Parse(payload, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (meassurement.Contains("MWh"))
            {
                currentValue = currentValue * 1000m;
            }
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
                },
                DateTimeOffset.UtcNow);
            break;
    }
}

void WriteEnphaseDataToDB(InfluxDB3Connector influx3Connector, string payload, string location, string device)
{
    try
    {
        var enphaseData = JsonSerializer.Deserialize<EnphaseData>(payload, jsonOptions);
        if (enphaseData != null)
        {
            var converters = app.Services.GetRequiredService<Converters>();
            influx3Connector.WriteInfluxRecords(converters.EnphaseDataToInfluxRecords(enphaseData, location, device));
        }
    }
    catch (Exception ex)
    {
        logger.LogError($"####Error deserializing Enphase data: {ex.Message}");
    }
}



GeoPosition? ExtractPositionFromPayload(string payload)
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
            logger.LogError("Latitude or longitude missing in position payload.");
        }
    }
    else
    {
        logger.LogError("Position object missing in payload.");
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