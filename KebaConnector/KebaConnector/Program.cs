// See https://aka.ms/new-console-template for more information

using HelpersLib;
using KebaConnector;
using MQTTnet;
using MQTTnet.Client;
using SharedContracts;
using System.Net;
using System.Text.Json;
using MQTTClient;

MQTTClient.MQTTClient mqttClient;
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

mqttClient = new MQTTClient.MQTTClient("KebaConnector");
Console.WriteLine($"    ClientId: {mqttClient.ClientId}");

mqttClient.OnMessageReceived += MqttMessageReceived;

await mqttClient.SubscribeToTopic("commands/charging/#");

Console.WriteLine("    ...Done");

var timer = new Timer(Update, null, 2000, 5000);

Thread.Sleep(Timeout.Infinite);


void Update(object? state)
{
    try
    {
        kebaOutside.ReadDeviceData().ContinueWith((task) =>
        {
            if (task.IsCompletedSuccessfully && task.Result is not null)
            {
                var data = task.Result;
                Console.WriteLine($"Keba Outside: {data.PlugStatus,-50} {data.CurrentChargingPower,10} W {data.EnergyCurrentChargingSession,15:#,##0} Wh {data.EnergyTotal,15:#,##0} Wh");
                SendDataAsMQTTMessage(mqttClient, data, "KebaOutside");
            }
        });

        kebaGarage.ReadDeviceData().ContinueWith((task) =>
        {
            if (task.IsCompletedSuccessfully && task.Result is not null)
            {
                var data = task.Result;
                Console.WriteLine($"Keba Garage : {data.PlugStatus,-50} {data.CurrentChargingPower,10} W {data.EnergyCurrentChargingSession,15:#,##0} Wh {data.EnergyTotal,15:#,##0} Wh");
                SendDataAsMQTTMessage(mqttClient, data, "KebaGarage");
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error reading device data -" + ex.ToDetailedString());
    }
}

async void MqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
{
    string payload = e.Payload;
    var topic = e.Topic;
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

static void SendDataAsMQTTMessage(MQTTClient.MQTTClient mqttClient, KebaData data, string device)
{
    var messageData = new ChargingGetData()
    {
        CarIsPlugedIn = data.PlugStatus == PlugStatus.CablePluggedInChargingStationAndVehicleAndLocked,
        CurrentChargingPower = data.CurrentChargingPower,
        EnergyCurrentChargingSession = data.EnergyCurrentChargingSession,
        EnergyTotal = data.EnergyTotal
    };
    mqttClient.PublishAsync($"data/charging/{device}", JsonSerializer.Serialize(messageData), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce, false);
}