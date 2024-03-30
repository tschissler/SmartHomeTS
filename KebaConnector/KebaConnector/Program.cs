// See https://aka.ms/new-console-template for more information

using HelpersLib;
using KebaConnector;
using MQTTnet;
using MQTTnet.Client;
using SharedContracts;
using System.Net;
using System.Text.Json;

IMqttClient mqttClient;
KebaDeviceConnector kebaOutside;
KebaDeviceConnector kebaGarage;

Console.WriteLine("KebaConnector started");

Console.WriteLine("  - Connecting to Keba Outside...");
var ipsOutside = await Dns.GetHostAddressesAsync("keba-stellplatz");
if (ipsOutside is null || ipsOutside.Length == 0)
{
    Console.WriteLine("    Could not resolve keba-stellplatz");
    return;
}
kebaOutside = new KebaDeviceConnector(ipsOutside[0], 7090);
Console.WriteLine("    ...Done");

Console.WriteLine("  - Connecting to Keba Garage...");
var ipsGarage = await Dns.GetHostAddressesAsync("keba-garage");
if (ipsGarage is null || ipsGarage.Length == 0)
{
    Console.WriteLine("    Could not resolve keba-garage");
    return;
}
kebaGarage = new KebaDeviceConnector(ipsGarage[0], 7090);
Console.WriteLine("    ...Done");

Console.WriteLine("  - Connecting to MQTT Broker");

var factory = new MqttFactory();
mqttClient = factory.CreateMqttClient();
await MQTTConnectAsync();
mqttClient.ApplicationMessageReceivedAsync += MqttMessageReceived;
await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("commands/charging/#").Build());

Console.WriteLine("    ...Done");

var timer = new Timer(Update, null, 0, 5000);

Thread.Sleep(Timeout.Infinite);


void Update(object? state)
{
    try
    {
        kebaOutside.ReadDeviceData().ContinueWith((task) =>
        {
            if (task.IsCompletedSuccessfully)
            {
                var data = task.Result;
                Console.WriteLine($"Keba Outside: {data.PlugStatus}, {data.CurrentChargingPower}W, {data.EnergyCurrentChargingSession}Wh, {data.EnergyTotal}Wh");
                SendDataAsMQTTMessage(mqttClient, data, "KebaOutside");
            }
        });

        kebaGarage.ReadDeviceData().ContinueWith((Action<Task<KebaData>>)((task) =>
        {
            if (task.IsCompletedSuccessfully)
            {
                var data = task.Result;
                Console.WriteLine($"Keba Garage : {data.PlugStatus}, {data.CurrentChargingPower}W, {data.EnergyCurrentChargingSession}Wh, {data.EnergyTotal}Wh");
                SendDataAsMQTTMessage(mqttClient, data, "KebaGarage");
            }
        }));
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error reading device data -" + ex.ToDetailedString());
    }
}

async Task MQTTConnectAsync()
{
    var mqttOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("smarthomepi2", 32004)
        .WithClientId("Smarthome.KebaConnector")
        .Build();

    while (true)
    {
        if (mqttClient.IsConnected)
            break;
        try
        {
            await mqttClient.ConnectAsync(mqttOptions);
            if (mqttClient.IsConnected)
            {
                Console.WriteLine("Connected to MQTT Broker.");
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

async Task MQTTDisconnectAsync()
{
    await mqttClient.DisconnectAsync();
}
async Task MqttMessageReceived(MqttApplicationMessageReceivedEventArgs args)
{
    string payload = args.ApplicationMessage.ConvertPayloadToString();
    var topic = args.ApplicationMessage.Topic;
    var time = DateTime.Now;

    Console.WriteLine($"Received message from {topic} at {time}: {payload}");

    ChargingSetData chargingSetData = null;
    try
    {
        chargingSetData = JsonSerializer.Deserialize<ChargingSetData>(payload);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to deserialize payload: {ex.Message}");
        return;
    }

    var topicparts = topic.Split("/");
    if (topicparts.Length < 3)
    {
        Console.WriteLine($"Invalid topic {topic}, topic needs to follow pattern [commands/Charging/<Device>]");
        return;
    }
    var kebaDevice = topic.Split("/")[2];
    if (kebaDevice.ToLower() == "kebagarage")
    {         
        await kebaGarage.UpdateDeviceDesiredCurrent(chargingSetData.ChargingCurrent);
    }
    else if (kebaDevice.ToLower() == "kebaoutside")
    {
        await kebaOutside.UpdateDeviceDesiredCurrent(chargingSetData.ChargingCurrent);
    }
    else
    {
        Console.WriteLine($"Unknown device {kebaDevice}");
    }

    return;
}

static void SendDataAsMQTTMessage(IMqttClient mqttClient, KebaData data, string device)
{
    var messageData = new ChargingGetData()
    {
        CarIsPlugedIn = data.PlugStatus == PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked,
        CurrentChargingPower = data.CurrentChargingPower,
        EnergyCurrentChargingSession = data.EnergyCurrentChargingSession,
        EnergyTotal = data.EnergyTotal
    };
    var message = new MqttApplicationMessageBuilder()
        .WithTopic($"data/charging/{device}")
        .WithPayload(JsonSerializer.Serialize(messageData))
        .WithRetainFlag()
        .Build();
    mqttClient.PublishAsync(message);
}