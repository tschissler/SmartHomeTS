using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using SmartHomeHelpers.Logging;
using System.Text;
using System.Text.Json;

namespace MQTTClient
{
    public class MQTTClient
    {
        private IMqttClient _client;
        private MqttClientOptions _options;
        private string _clientId;

        public event EventHandler<MqttMessageReceivedEventArgs> OnMessageReceived;

        public MQTTClient(string clientId)
        {
            _clientId = clientId;
            Task.Run(async () => await ConnectAsync()).Wait();
        }

        async Task ConnectAsync()
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
            .WithTcpServer("smarthomepi2", 32004)
            .WithClientId(_clientId)
            .WithKeepAlivePeriod(new TimeSpan(0, 1, 0, 0))
            .Build();

            _client.ApplicationMessageReceivedAsync += e =>
            {
                var messageReceivedEventArgs = new MqttMessageReceivedEventArgs
                {
                    Topic = e.ApplicationMessage.Topic,
                    Payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
                };
                OnMessageReceived?.Invoke(this, messageReceivedEventArgs);
                return Task.CompletedTask;
            };

            await _client.ConnectAsync(_options);
        }

        public async Task MQTTDisconnectAsync()
        {
            await _client.DisconnectAsync();
        }

        public async Task SubscribeToTopic(string topic)
        {
            if (!_client.IsConnected)
            {
                await ConnectAsync();
            }
            await _client.SubscribeAsync(topic);
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
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
}
