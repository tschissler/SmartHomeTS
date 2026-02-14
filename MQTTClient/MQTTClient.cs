using MQTTnet;
using MQTTnet.Protocol;
using SmartHomeHelpers.Logging;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MQTTClient
{
    public class MQTTClient : IDisposable
    {
        private IMqttClient _client;
        private MqttClientOptions _options;
        public string ClientId { get; init; }
        public string BrokerName { get; init; }
        public int BrokerPort { get; init; }
        public bool IsConnected => _client.IsConnected;
        private DateTimeOffset lastMessageReceived = DateTimeOffset.UtcNow;

        public event EventHandler<MqttMessageReceivedEventArgs> OnMessageReceived;

        /// <summary>
        /// Returns the time since the last MQTT message was received
        /// </summary>
        public TimeSpan TimeSinceLastMessage => DateTimeOffset.UtcNow - lastMessageReceived;

        public MQTTClient(string clientId, string brokerName, int brokerPort)
        {
            ClientId = clientId + "_" + Environment.MachineName;
            BrokerName = brokerName;
            BrokerPort = brokerPort;
            Task.Run(async () => await ConnectAsync()).Wait();
        }

        public async Task ConnectAsync()
        {
            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
            .WithTcpServer(BrokerName, BrokerPort)
            .WithClientId(ClientId)
            .WithKeepAlivePeriod(new TimeSpan(0, 1, 0, 0))
            .Build();

            _client.ApplicationMessageReceivedAsync += e =>
            {
                if (e.ApplicationMessage.Payload.Length > 0)
                {
                    lastMessageReceived = DateTimeOffset.UtcNow;
                    var messageReceivedEventArgs = new MqttMessageReceivedEventArgs
                    {
                        Topic = e.ApplicationMessage.Topic,
                        Payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
                    };
                    OnMessageReceived?.Invoke(this, messageReceivedEventArgs);
                }
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

        public async void Dispose()
        {
            await MQTTDisconnectAsync();
        }
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
}
