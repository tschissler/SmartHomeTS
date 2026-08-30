using MQTTnet;
using MQTTnet.Protocol;
using SharedContracts;
using System.Globalization;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartHome.Web.Services
{
    public class MqttService
    {
        private IMqttClient _client;
        private MqttClientOptions _options;

        public event EventHandler<MqttMessageReceivedEventArgs> OnMessageReceived;
        public ChargingSettings ChargingSettings { get; set; }
        public ChargingSituation ChargingSituation { get; set; }
        public IluminationSituation IluminationSituation { get; set; }
        public ClimateData ClimateData { get; set; }
        public CarStatusData BmwStatusData { get; set; }
        public CarStatusData MiniStatusData { get; set; }
        public CarStatusData VwStatusData { get; set; }
        public HeatingCommandData? HeatingKinderzimmerCommand { get; private set; }
        public HeatingCommandData? HeatingEsszimmerCommand { get; private set; }
        public bool IsConnected => _client?.IsConnected ?? false;

        /// <summary>Heartbeats keyed by status topic. Devices appear here as soon as they report one.</summary>
        public ConcurrentDictionary<string, DeviceStatus> Devices { get; } = new();

        /// <summary>Firmware version currently offered per device type, taken from the OTA topics.</summary>
        public ConcurrentDictionary<string, string> OfferedVersions { get; } = new();

        /// <summary>Devices that only publish a version topic - they still run firmware without a heartbeat.</summary>
        public ConcurrentDictionary<string, LegacyDevice> LegacyDevices { get; } = new();

        /// <summary>Acknowledged reset warnings: device key -> boot epoch the user confirmed.
        /// Retained on the broker, so the acknowledgment holds across clients and restarts.</summary>
        public ConcurrentDictionary<string, long> ResetAcks { get; } = new();

        private static readonly Regex VersionInUrl = new(@"_([0-9]+(?:\.[0-9]+)+)\.bin", RegexOptions.Compiled);
        private static readonly Regex PlainVersion = new(@"^[0-9]+(?:\.[0-9]+)+$", RegexOptions.Compiled);


        public MqttService()
        {
            ChargingSettings = new();
            ChargingSituation = new();
            IluminationSituation = new();
            ClimateData = new();

            ConnectAsync();
        }

        public async Task ConnectAsync()
        {
            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
            .WithTcpServer("mosquitto.intern", 1883)
            .WithClientId("Smarthome.Web")
            .WithKeepAlivePeriod(new TimeSpan(0, 1, 0, 0))
            .Build();

            _client.ConnectedAsync += async e =>
            {
                Console.WriteLine("Connected to MQTT broker.");
                await _client.SubscribeAsync("data/#");
                await _client.SubscribeAsync("daten/#");
                await _client.SubscribeAsync("config/charging/settings");
                await _client.SubscribeAsync("commands/illumination/LEDStripe/setColor");
                await _client.SubscribeAsync("commands/shelly/Lampe");
                await _client.SubscribeAsync("commands/Heating/#");
                await _client.SubscribeAsync("status/#");
                await _client.SubscribeAsync("config/DeviceMonitor/ack/#");
                await _client.SubscribeAsync("OTAUpdate/#");
                await _client.SubscribeAsync("meta/#");
            };

            _client.DisconnectedAsync += async e =>
            {
                Console.WriteLine($"Disconnected from MQTT broker. Reason: {e.Reason}. Reconnecting in 5s...");
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    await _client.ConnectAsync(_options, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MQTT reconnection attempt failed: {ex.Message}");
                }
            };

            _client.ApplicationMessageReceivedAsync += e =>
            {
                UpdateSharedData(e.ApplicationMessage);
                var messageReceivedEventArgs = new MqttMessageReceivedEventArgs
                {
                    Topic = e.ApplicationMessage.Topic
                };
                OnMessageReceived?.Invoke(this, messageReceivedEventArgs);
                return Task.CompletedTask;
            };

            await _client.ConnectAsync(_options, CancellationToken.None);
        }

        public async Task PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos, bool retain)
        {
            if (!_client.IsConnected)
            {
                throw new InvalidOperationException("MQTT client is not connected. Reconnection is in progress.");
            }
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(qos)
                .WithRetainFlag(retain)
                .Build();

            await _client.PublishAsync(message, CancellationToken.None);
        }
        private void UpdateSharedData(MqttApplicationMessage message)
        {
            //Console.WriteLine("Received message on topic: " + message.Topic);

            // An empty retained payload deletes a topic - that is how a device is removed from
            // the list, so it has to be handled before the empty-payload guard below.
            if (message.Payload.Length == 0)
            {
                if (message.Topic.StartsWith("status/"))
                {
                    Devices.TryRemove(message.Topic, out _);
                }
                else if (message.Topic.StartsWith("meta/"))
                {
                    LegacyDevices.TryRemove(message.Topic, out _);
                }
                else if (message.Topic.StartsWith("config/DeviceMonitor/ack/"))
                {
                    ResetAcks.TryRemove(message.Topic["config/DeviceMonitor/ack/".Length..], out _);
                }
                return;
            }

            var payload = Encoding.UTF8.GetString(message.Payload);

            if (TrackDeviceTopics(message.Topic, payload))
            {
                return;
            }

            switch (message.Topic)
            {
                case "data/charging/BMW":
                    {
                        BmwStatusData = JsonSerializer.Deserialize<CarStatusData>(payload);
                        break;
                    }
                case "data/charging/Mini":
                    {
                        MiniStatusData = JsonSerializer.Deserialize<CarStatusData>(payload);
                        break;
                    }
                case "data/charging/VW":
                    {
                        VwStatusData = JsonSerializer.Deserialize<CarStatusData>(payload);
                        break;
                    }
                case "data/charging/situation":
                    {
                        ChargingSituation = JsonSerializer.Deserialize<ChargingSituation>(payload);
                        break;
                    }
                case "config/charging/settings":
                    {
                        ChargingSettings = JsonSerializer.Deserialize<ChargingSettings>(payload);
                        break;
                    }
                case "commands/illumination/LEDStripe/setColor":
                    {
                        IluminationSituation = JsonSerializer.Deserialize<IluminationSituation>(payload);
                        break;
                    }
                case "daten/temperatur/M1/Keller":
                    {
                        ClimateData.BasementTemperature = CreateDataPoint(payload) ?? ClimateData.BasementTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/M1/Keller":
                    {
                        ClimateData.BasementHumidity = CreateDataPoint(payload) ?? ClimateData.BasementHumidity;
                        break;
                    }
                case "daten/zisterneFuellstand/M1/Keller":
                    {
                        ClimateData.CisternFillLevel = CreateDataPoint(payload) ?? ClimateData.CisternFillLevel;
                        break;
                    }
                case "daten/temperatur/M1/Aussen":
                    {
                        ClimateData.OutsideTemperature = CreateDataPoint(payload) ?? ClimateData.OutsideTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/M1/Aussen":
                    {
                        ClimateData.OutsideHumidity = CreateDataPoint(payload) ?? ClimateData.OutsideHumidity;
                        break;
                    }
                case "daten/temperatur/M1/Kinderzimmer":
                {
                    ClimateData.ChildRoomTemperature = CreateDataPoint(payload) ?? ClimateData.ChildRoomTemperature;
                    break;
                }
                case "daten/luftfeuchtigkeit/M1/Kinderzimmer":
                {
                    ClimateData.ChildRoomHumidity = CreateDataPoint(payload) ?? ClimateData.ChildRoomHumidity;
                    break;
                }
                case "daten/temperatur/M1/Bad":
                {
                    ClimateData.BathRoomM1Temperature = CreateDataPoint(payload) ?? ClimateData.BathRoomM1Temperature;
                    break;
                }
                case "daten/luftfeuchtigkeit/M1/Bad":
                {
                    ClimateData.BathRoomM1Humidity = CreateDataPoint(payload) ?? ClimateData.BathRoomM1Humidity;
                    break;
                }
                case "daten/temperatur/M1/Wohnzimmer":
                    {
                        ClimateData.LivingRoomTemperature = CreateDataPoint(payload) ?? ClimateData.LivingRoomTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/M1/Wohnzimmer":
                    {
                        ClimateData.LivingRoomHumidity = CreateDataPoint(payload) ?? ClimateData.LivingRoomHumidity;
                        break;
                    }
                case "daten/temperatur/M1/Schlafzimmer":
                    {
                        ClimateData.BedroomTemperature = CreateDataPoint(payload) ?? ClimateData.BedroomTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/M1/Schlafzimmer":
                    {
                        ClimateData.BedroomHumidity = CreateDataPoint(payload) ?? ClimateData.BedroomHumidity;
                        break;
                    }
                case "commands/shelly/Lampe":
                    {
                        if (payload.ToLower() == "toggle")
                        {
                            IluminationSituation.LampOn = !IluminationSituation.LampOn;
                        }
                        else
                            IluminationSituation.LampOn = payload == "on";
                        break;
                    }
                case "commands/Heating/Heizkörperlüfter_Kinderzimmer":
                    {
                        HeatingKinderzimmerCommand = JsonSerializer.Deserialize<HeatingCommandData>(payload);
                        break;
                    }
                case "commands/Heating/Heizkörperlüfter_Esszimmer":
                    {
                        HeatingEsszimmerCommand = JsonSerializer.Deserialize<HeatingCommandData>(payload);
                        break;
                    }
            }
        }

        /// <summary>
        /// Handles the topics that describe devices rather than measurements.
        /// Returns true when the message was consumed here.
        /// </summary>
        private bool TrackDeviceTopics(string topic, string payload)
        {
            if (topic.StartsWith("status/"))
            {
                try
                {
                    var status = JsonSerializer.Deserialize<DeviceStatus>(payload,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (status is not null)
                    {
                        status.RawPayload = payload;
                        // An unchanged repeat is a retained replay, not a sign of life
                        var unchanged = Devices.TryGetValue(topic, out var known) && known.RawPayload == payload;
                        status.ReceivedAt = unchanged ? known!.ReceivedAt : DateTimeOffset.Now;
                        Devices[topic] = status;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Ignoring malformed heartbeat on {topic}: {ex.Message}");
                }
                return true;
            }

            if (topic.StartsWith("config/DeviceMonitor/ack/"))
            {
                if (long.TryParse(payload.Trim(), out var bootEpoch))
                {
                    ResetAcks[topic["config/DeviceMonitor/ack/".Length..]] = bootEpoch;
                }
                return true;
            }

            if (topic.StartsWith("OTAUpdate/"))
            {
                var deviceType = topic["OTAUpdate/".Length..];
                var match = VersionInUrl.Match(payload);
                if (match.Success)
                {
                    OfferedVersions[deviceType] = match.Groups[1].Value;
                }
                return true;
            }

            if (topic.StartsWith("meta/") && PlainVersion.IsMatch(payload.Trim()))
            {
                LegacyDevices[topic] = new LegacyDevice
                {
                    Topic = topic,
                    Version = payload.Trim(),
                    ReceivedAt = DateTimeOffset.Now
                };
                return true;
            }

            return false;
        }

        private DataPoint? CreateDataPoint(string payload)
        {
            if (Decimal.TryParse(payload, new CultureInfo("en-US"), out var value))
            {
                return new DataPoint(value, DateTimeOffset.Now);
            }

            return null; 
        }
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
    }
}
