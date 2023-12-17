using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using System.Globalization;

namespace SmartHome.Web.Components.Libs
{
    public class MqttDataPoint
    {
        public DateTime Time { get; set; }
        public decimal Value { get; set; }
    }

    public class MqttController
    {
        private IMqttClient _client;
        private MqttClientOptions _options;

        public event Action OnDataUpdated;

        public Dictionary<string, List<MqttDataPoint>> MqttData { get; set; } = new ();

        public MqttController()
        {
            // Initialize MQTT Client
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            // Configure options (adjust the settings as needed)
            _options = new MqttClientOptionsBuilder()
                .WithTcpServer("smarthomepi2", 32004) // Use your MQTT Broker's address and port
                .WithClientId("Smarthome.Web")
                .Build();

            // Handle received messages
            _client.ApplicationMessageReceivedAsync += _client_ApplicationMessageReceivedAsync;

            // Connect to the broker
            ConnectAsync();
        }

        private Task _client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            Console.WriteLine("Received MQTT message: " + args.ApplicationMessage.Topic + " - " + args.ApplicationMessage.ConvertPayloadToString());
            var topic = args.ApplicationMessage.Topic;
            var value = decimal.Parse(args.ApplicationMessage.ConvertPayloadToString(), new CultureInfo("en-US"));
            var time = DateTime.Now;

            if (MqttData.ContainsKey(topic))
            {
                MqttData[topic].Add(new MqttDataPoint { Time = time, Value = value });
            }
            else
            {
                MqttData.Add(topic, new List<MqttDataPoint> { new MqttDataPoint { Time = time, Value = value } });
            }

            if (MqttData[topic].Count > 5000)
            {
                MqttData[topic].Remove(MqttData[topic].First());
            }

            OnDataUpdated?.Invoke();

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
                    await _client.SubscribeAsync("M3/1c50f3ab6224/temperature");
                    await _client.SubscribeAsync("M3/1c50f3ab6224/humidity");
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
