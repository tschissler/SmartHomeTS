// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTClient;
using Newtonsoft.Json;
using ShellyConnector;
using SmartHomeHelpers.Logging;

ConsoleHelpers.PrintInformation("ShellyConnector started");

ConsoleHelpers.PrintInformation(" ### Registering services");
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<MQTTClient.MQTTClient>(provider => new MQTTClient.MQTTClient("ShellyConnector"));
    })
    .Build();

using var serviceScope = host.Services.CreateScope();
var services = serviceScope.ServiceProvider;

var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();

List<ShellyConnector.DataContracts.ShellyDevice> powerDevices, thermostatDevices;

powerDevices = ShellyDevices.GetPowerDevices();
thermostatDevices = ShellyDevices.GetThermostatDevices();

await TestDeviceConnection(powerDevices, thermostatDevices);

ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
await mqttClient.SubscribeToTopic("commands/shelly/#");
mqttClient.OnMessageReceived += (sender, e) =>
{
    ConsoleHelpers.PrintInformation($"  - Message received: {e.Topic}");
    if (string.IsNullOrEmpty(e.Payload))
    {
        ConsoleHelpers.PrintErrorMessage($"Empty payload for message on topic {e.Topic}");
        return;
    }

    var topicParts = e.Topic.Split("/");
    if (topicParts.Length < 2) return;
    var deviceName = topicParts[2];
    var device = powerDevices.FirstOrDefault(x =>
        string.Equals(x.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    if (device != null)
    {
        var state = e.Payload;
        ShellyConnector.ShellyConnector.SetRelay(device, state);
        return;
    }

    if (topicParts.Length < 3) return;
    var location = topicParts[2];
    deviceName = topicParts[3];
    device = thermostatDevices.FirstOrDefault(x =>
        string.Equals(x.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(x.Location.ToString(), location, StringComparison.OrdinalIgnoreCase));
    if (device != null)
    {
        ShellyConnector.ShellyConnector.SetTargetTemp(device, e.Payload);
        return;
    }
};

ConsoleHelpers.PrintInformation(" ### Creating timer to read data from devices every second");
ConsoleHelpers.PrintInformation("     and send the data as MQTT messages");
var timerPowerDevices = new System.Timers.Timer(1000);
timerPowerDevices.Elapsed += async (sender, e) =>
{
    var tasks = powerDevices.Where(i => i.IsConnected).Select(async device =>
    {
        var powerData = await ShellyConnector.ShellyConnector.GetPowerData(device);
        if (powerData is null)
        {
            ConsoleHelpers.PrintInformation($"  --- Could not read meter data from device {device.DeviceName}");
            return;
        }
        if (!powerData.IsValid)
        {
            ConsoleHelpers.PrintInformation($"  --- Data from device {device.DeviceName} is invalid");
            return;
        }
        var jsonPayload = JsonConvert.SerializeObject(powerData);
        await mqttClient.PublishAsync($"data/electricity/{device.Location}/shelly/{device.DeviceName}", jsonPayload, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, false);
    });

    await Task.WhenAll(tasks);
};
timerPowerDevices.Start();

var timerThermostatDevices = new System.Timers.Timer(60000);
timerThermostatDevices.Elapsed += async (sender, e) =>
{
    var tasks = thermostatDevices.Where(i => i.IsConnected).Select(async device =>
    {
        var thermostatData = await ShellyConnector.ShellyConnector.GetThermostatData(device);
        if (thermostatData is null)
        {
            ConsoleHelpers.PrintInformation($"  --- Could not read meter data from device {device.DeviceName}");
            return;
        }
        var jsonPayload = JsonConvert.SerializeObject(thermostatData);
        await mqttClient.PublishAsync($"data/thermostat/{device.Location}/shelly/{device.DeviceName}", jsonPayload, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, false);
    });

    await Task.WhenAll(tasks);
};
timerThermostatDevices.Start();

var timerTestDeviceConnection = new System.Timers.Timer(300000);
timerTestDeviceConnection.Elapsed += async (sender, e) =>
{
    await TestDeviceConnection(powerDevices, thermostatDevices);
};
timerTestDeviceConnection.Start();

ConsoleHelpers.PrintInformation("");
ConsoleHelpers.PrintInformation(" ### Done");

// Run the host to keep the application running and processing events
await host.RunAsync();

static async Task TestDeviceConnection(List<ShellyConnector.DataContracts.ShellyDevice> powerDevices, List<ShellyConnector.DataContracts.ShellyDevice> thermostatDevices)
{
    ConsoleHelpers.PrintInformation(" ### Testing connection to all Power-Devices");

    foreach (var powerDevice in powerDevices)
    {
        var status = await ShellyConnector.ShellyConnector.GetPowerStaus(powerDevice);
        if (status is null)
            ConsoleHelpers.PrintInformation($"  --- Could not connect to powerDevice {powerDevice.DeviceName}");
        else
        {
            powerDevice.IsConnected = true;
            ConsoleHelpers.PrintInformation($"  - {powerDevice.DeviceName,-30} {"(" + powerDevice.IPAddress + ")",-20} {status.app + status.type,20} => success");
        }
    }

    ConsoleHelpers.PrintInformation(" ### Testing connection to all Thermostat-Devices");

    foreach (var thermostatDevice in thermostatDevices)
    {
        var status = await ShellyConnector.ShellyConnector.GetThermostatStaus(thermostatDevice);
        if (status is null)
            ConsoleHelpers.PrintInformation($"  --- Could not connect to thermostatDevice {thermostatDevice.DeviceName}");
        else
        {
            thermostatDevice.IsConnected = true;
            ConsoleHelpers.PrintInformation($"  - {thermostatDevice.DeviceName,-30} {"(" + thermostatDevice.IPAddress + ")",-20} => success");
        }
    }
}