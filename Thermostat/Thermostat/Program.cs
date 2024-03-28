using MQTTControllerLib;
using MQTTnet.Client;
using MQTTnet;
using ShellyLib;
using System.Net;
using System.Globalization;

var shellyPlus = new ShellyDevice { 
    IPAddress = new IPAddress([192, 168, 178, 197]), 
    DeviceType = DeviceType.ShellyPlugS };

Console.WriteLine("Thermostat Controller");
Console.WriteLine(" --> Connecting to MQTT Broker ...");
//var mqttController = new MqttController("smarthomepi2", 32004, "Smarthome.StorageConnector", "data/#");
//mqttController.OnDataUpdated += MqttController_OnDataUpdated;

var factory = new MqttFactory();
var _client = factory.CreateMqttClient();

var _options = new MqttClientOptionsBuilder()
    .WithTcpServer("smarthomepi2", 32004)
    .WithClientId("Smarthome.ThermostatConnector")
.Build();


_client.ConnectAsync(_options).Wait();
// Handle received messages
_client.ApplicationMessageReceivedAsync += MqttController_OnDataUpdated;

_client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("data/a86d2b286f24/temperature").Build());
Console.WriteLine("     Done");

Thread.Sleep(Timeout.Infinite);

Task MqttController_OnDataUpdated(MqttApplicationMessageReceivedEventArgs args)
{
    string payload = args.ApplicationMessage.ConvertPayloadToString();
    var topic = args.ApplicationMessage.Topic;
    var time = DateTime.Now;

    double value = 0;
    double.TryParse(payload, new CultureInfo("en-US"), out value);

    Console.WriteLine($"Received data from topic {topic}: {value} at {time}");
    if (value < 25)
    {
        ShellyConnector.TurnRelayOn(shellyPlus);
    }
    if (value > 26)
    {
        ShellyConnector.TurnRelayOff(shellyPlus);
    }
    return Task.CompletedTask;
}