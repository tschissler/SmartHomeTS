using MQTTnet;
using MQTTnet.Protocol;
using SharedContracts;
using SmartHome.Web.Components.Pages;
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
                case "data/keller/temperature":
                    {
                        ClimateData.BasementTemperature = CreateDataPoint(payload) ?? ClimateData.BasementTemperature;
                        break;
                    }
                case "data/keller/humidity":
                    {
                        ClimateData.BasementHumidity = CreateDataPoint(payload) ?? ClimateData.BasementHumidity;
                        break;
                    }
                case "data/keller/cisternFillLevel":
                    {
                        ClimateData.CisternFillLevel = CreateDataPoint(payload) ?? ClimateData.CisternFillLevel;
                        break;
                    }
                case "daten/temperatur/Aussen":
                    {
                        ClimateData.OutsideTemperature = CreateDataPoint(payload) ?? ClimateData.OutsideTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/Aussen":
                    {
                        ClimateData.OutsideHumidity = CreateDataPoint(payload) ?? ClimateData.OutsideHumidity;
                        break;
                    }
                case "daten/temperatur/Wohnzimmer":
                    {
                        ClimateData.LivingRoomTemperature = CreateDataPoint(payload) ?? ClimateData.LivingRoomTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/Wohnzimmer":
                    {
                        ClimateData.LivingRoomHumidity = CreateDataPoint(payload) ?? ClimateData.LivingRoomHumidity;
                        break;
                    }
                case "daten/temperatur/Schlafzimmer":
                    {
                        ClimateData.BedroomTemperature = CreateDataPoint(payload) ?? ClimateData.BedroomTemperature;
                        break;
                    }
                case "daten/luftfeuchtigkeit/Schlafzimmer":
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
