using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using Newtonsoft.Json;
using System.Globalization;

namespace MQTTControllerLib
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
            // Initialize the  MQTT Client
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBrokerName, mqttBrokerPort)
                .WithClientId(clientName)
                .Build();

            // Handle received messages
            _client.ApplicationMessageReceivedAsync += _client_ApplicationMessageReceivedAsync;
            _client.ConnectedAsync += _client_ConnectedAsync;
            _client.DisconnectedAsync += _client_DisconnectedAsync;
            
            ConnectAsync().Wait();
        }

        public MqttController(string mqttBrokerName, int mqttBrokerPort, string clientName, string topic)
        {
            // Initialize the  MQTT Client
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttBrokerName, mqttBrokerPort)
                .WithClientId(clientName)
            .Build();


            _client.ConnectAsync(_options).Wait();
            // Handle received messages
            _client.ApplicationMessageReceivedAsync += _client_ApplicationMessageReceivedAsync;
            _client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());
            //_client.ConnectedAsync += _client_ConnectedAsync;
            //_client.DisconnectedAsync += _client_DisconnectedAsync;

        }

        private Task _client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Console.WriteLine(" --- Has been disconnected from the MQTT Broker. Reason: " + arg.ReasonString);
            Console.WriteLine(" --- Trying to reconnect ...");

            ConnectAsync().Wait();

            return Task.CompletedTask;
        }

        private Task _client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Console.WriteLine(" --- Connected to MQTT Broker");
            return Task.CompletedTask;
        }

        private Task _client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            string payload = args.ApplicationMessage.ConvertPayloadToString();
            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} --- Received MQTT message: {args.ApplicationMessage.Topic} - {payload}");
            var topic = args.ApplicationMessage.Topic;
            var time = DateTime.Now;

            double value = 0;
            if (topic.StartsWith("data/watermeter"))
            {
                if (topic == "data/watermeter/main/json")
                {
                    var data = JsonConvert.DeserializeObject<WatermeterData>(payload);
                    if (double.TryParse(data.raw, new CultureInfo("en-US"), out value))
                    {
                        OnDataUpdated?.Invoke(topic, value, data.timestamp.ToUniversalTime());
                    }
                }
                return Task.CompletedTask;
            }

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
            while (true)
            {
                if (_client.IsConnected)
                    break;
                try
                {
                    await _client.ConnectAsync(_options);
                    if (_client.IsConnected)
                    {
                        Console.WriteLine("Connected to MQTT Broker.");
                        // Subscribe to topics here
                        await _client.SubscribeAsync("data/#");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to MQTT Broker: {ex.Message}");
                    Thread.Sleep(5000);
                }
            }
        }

        public async Task DisconnectAsync()
        {
            await _client.DisconnectAsync();
        }
    }
}
