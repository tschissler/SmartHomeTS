using MQTTnet;
using MQTTnet.Client;
using System.Globalization;

namespace MqttLib
{
    public class MqttController
    {
        private IMqttClient _client;
        private MqttClientOptions _options;

        public event DataUpdatedHandler OnDataUpdated;
        public event StringDataUpdatedHandler OnStringDataUpdated;

        public delegate void DataUpdatedHandler(string topic, double value, DateTime time);
        public delegate void StringDataUpdatedHandler(string topic, string value, DateTime time);

        public MqttController(string mqttBrokerName, int mqttBrokerPort, string clientName)
        {
            // Initialize MQTT Client
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBrokerName, mqttBrokerPort) 
                .WithClientId(clientName)
                .Build();

            // Connect to the broker
            ConnectAsync();
        }

        public async Task SendMessage(string topic, double payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload.ToString(new CultureInfo("en-US")))
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                .Build();

            await _client.PublishAsync(message);
        }
       
        public async Task ConnectAsync()
        {
            try
            {
                await _client.ConnectAsync(_options);
                if (_client.IsConnected)
                {
                    Console.WriteLine("Connected to MQTT Broker.");
                    // Subscribe to topics here
                    await _client.SubscribeAsync("data/#");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to MQTT Broker: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            await _client.DisconnectAsync();
        }
    }
}
