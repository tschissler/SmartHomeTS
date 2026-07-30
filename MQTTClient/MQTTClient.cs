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
        private readonly IMqttClient _client;
        private readonly MqttClientOptions _options;
        private readonly HashSet<string> _subscribedTopics = new();
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private volatile bool _closing = false;
        public string ClientId { get; init; }
        public string BrokerName { get; init; }
        public int BrokerPort { get; init; }
        public bool IsConnected => _client.IsConnected;
        private DateTimeOffset lastMessageReceived = DateTimeOffset.UtcNow;

        public event EventHandler<MqttMessageReceivedEventArgs> OnMessageReceived;

        /// <summary>
        /// Raised with false when the broker connection is lost and with true once it is
        /// re-established (subscriptions are restored before the event fires).
        /// </summary>
        public event EventHandler<bool>? OnConnectionStateChanged;

        /// <summary>
        /// Returns the time since the last MQTT message was received
        /// </summary>
        public TimeSpan TimeSinceLastMessage => DateTimeOffset.UtcNow - lastMessageReceived;

        public MQTTClient(string clientId, string brokerName, int brokerPort)
        {
            ClientId = clientId + "_" + Environment.MachineName;
            BrokerName = brokerName;
            BrokerPort = brokerPort;

            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(BrokerName, BrokerPort)
                .WithClientId(ClientId)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
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

            _client.DisconnectedAsync += HandleDisconnectedAsync;

            Task.Run(async () => await ConnectAsync()).Wait();
        }

        public async Task ConnectAsync()
        {
            if (_client.IsConnected || _closing)
                return;

            await _connectLock.WaitAsync();
            try
            {
                if (_client.IsConnected || _closing)
                    return;

                await _client.ConnectAsync(_options);

                string[] topics;
                lock (_subscribedTopics)
                {
                    topics = _subscribedTopics.ToArray();
                }
                foreach (var topic in topics)
                {
                    await _client.SubscribeAsync(topic);
                }
            }
            finally
            {
                _connectLock.Release();
            }

            OnConnectionStateChanged?.Invoke(this, true);
        }

        private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            if (_closing)
                return;

            Console.WriteLine($"MQTT connection to {BrokerName} lost ({e.Reason}), reconnecting...");
            OnConnectionStateChanged?.Invoke(this, false);

            while (!_closing && !_client.IsConnected)
            {
                try
                {
                    await ConnectAsync();
                    Console.WriteLine($"MQTT connection to {BrokerName} re-established, subscriptions restored");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MQTT reconnect to {BrokerName} failed: {ex.Message}, retrying in 5s");
                    await Task.Delay(5000);
                }
            }
        }

        public async Task MQTTDisconnectAsync()
        {
            _closing = true;
            await _client.DisconnectAsync();
        }

        public async Task SubscribeToTopic(string topic)
        {
            lock (_subscribedTopics)
            {
                _subscribedTopics.Add(topic);
            }
            if (!_client.IsConnected)
            {
                // ConnectAsync restores all tracked subscriptions, including this one
                await ConnectAsync();
                return;
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

        public void Dispose()
        {
            _closing = true;
            try
            {
                if (_client.IsConnected)
                    _client.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort on shutdown
            }
            _client.Dispose();
        }
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
}
