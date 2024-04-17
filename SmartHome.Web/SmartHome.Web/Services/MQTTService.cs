using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using SharedContracts;
using SmartHome.Web.Components.Pages;
using System.Text;
using System.Text.Json;

namespace SmartHome.Web.Services
{
    public class MQTTService
    {
        private IMqttClient _client;
        private MqttClientOptions _options;

        public event EventHandler<MqttMessageReceivedEventArgs> OnMessageReceived;
        public SharedContracts.ChargingSettings ChargingSettings { get; set; }
        public ChargingSituation ChargingSituation { get; set; }

        public MQTTService()
        {
            ChargingSettings = new();
            ChargingSituation = new();
            Task task = ConnectAsync();
        }

        public async Task ConnectAsync()
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
            .WithTcpServer("smarthomepi2", 32004)
            .WithClientId("Smarthome.Web")
            .WithKeepAlivePeriod(new TimeSpan(0, 1, 0, 0))
            .Build();

            _client.ConnectedAsync += async e =>
            {
                Console.WriteLine("Connected to MQTT broker.");
                await _client.SubscribeAsync("data/charging/situation");
                await _client.SubscribeAsync("config/charging/settings");
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
            Console.WriteLine("Received message on topic: " + message.Topic);
            if (message.Topic == "data/charging/situation")
            {
                var payload = Encoding.UTF8.GetString(message.Payload);
                ChargingSituation = JsonSerializer.Deserialize<ChargingSituation>(payload);
            }
            else if (message.Topic == "config/charging/settings")
            {
                var payload = Encoding.UTF8.GetString(message.Payload);
                ChargingSettings = JsonSerializer.Deserialize<ChargingSettings>(payload);
            }
        }
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
    }
}
