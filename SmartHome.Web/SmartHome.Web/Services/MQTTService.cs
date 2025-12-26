using MQTTnet;
using MQTTnet.Protocol;
using SharedContracts;
using System.Globalization;
using System.Text;
using System.Text.Json;

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
            .WithTcpServer("smarthomepi2", 32004)
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
                await ConnectAsync();
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

            if (message.Payload.Length == 0)
            {
                return;
            }

            var payload = Encoding.UTF8.GetString(message.Payload);

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
