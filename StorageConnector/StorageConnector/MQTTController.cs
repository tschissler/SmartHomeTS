using MQTTnet.Client;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageConnector
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

            // Handle received messages
            _client.ApplicationMessageReceivedAsync += _client_ApplicationMessageReceivedAsync;

            // Connect to the broker
            ConnectAsync();
        }

        private Task _client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            string payload = args.ApplicationMessage.ConvertPayloadToString();
            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} --- Received MQTT message: {args.ApplicationMessage.Topic} - {payload}");
            var topic = args.ApplicationMessage.Topic;
            var time = DateTime.Now;

            double value = 0;
            if (double.TryParse(payload, new CultureInfo("en-US"), out value))
            {
                OnDataUpdated?.Invoke(topic, value, time);
            }
            else
            {
                OnStringDataUpdated?.Invoke(topic, payload, time);
            }

            return Task.CompletedTask;
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
