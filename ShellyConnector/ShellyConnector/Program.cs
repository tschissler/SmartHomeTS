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

ConsoleHelpers.PrintInformation(" ### Testing connection to all Power-Devices");

var powerDevices = ShellyDevices.GetPowerDevices();
foreach (var powerDevice in powerDevices)
{
    var status = ShellyConnector.ShellyConnector.GetPowerStaus(powerDevice);
    if (status is null)
        ConsoleHelpers.PrintInformation($"  --- Could not connect to powerDevice {powerDevice.DeviceName}");
    else
        ConsoleHelpers.PrintInformation($"  - {powerDevice.DeviceName,-30} {"(" + powerDevice.IPAddress + ")",-20} {status.app + status.type,20} => success");
}

ConsoleHelpers.PrintInformation(" ### Testing connection to all Thermostat-Devices");

var thermostatDevices = ShellyDevices.GetThermostatDevices();
foreach (var thermostatDevice in thermostatDevices)
{
    var status = ShellyConnector.ShellyConnector.GetThermostatStaus(thermostatDevice);
    if (status is null)
        ConsoleHelpers.PrintInformation($"  --- Could not connect to thermostatDevice {thermostatDevice.DeviceName}");
    else
        ConsoleHelpers.PrintInformation($"  - {thermostatDevice.DeviceName,-30} {"(" + thermostatDevice.IPAddress + ")",-20} => success");
}

ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
await mqttClient.SubscribeToTopic("commands/shelly/#");
mqttClient.OnMessageReceived += (sender, e) =>
{
    ConsoleHelpers.PrintInformation($"  - Message received: {e.Topic}");
    var deviceName = e.Topic.Split("/")[2];
    var state = e.Payload;
    ShellyConnector.ShellyConnector.SetRelay(powerDevices.First(i => i.DeviceName == deviceName), state);
};

ConsoleHelpers.PrintInformation(" ### Creating timer to read data from devices every second");
ConsoleHelpers.PrintInformation("     and send the data as MQTT messages");
var timerPowerDevices = new System.Timers.Timer(60000);
timerPowerDevices.Elapsed += async (sender, e) =>
{
    var tasks = powerDevices.Select(async device =>
    {
        var powerData = ShellyConnector.ShellyConnector.GetPowerData(device);
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

var timerThermostatDevices = new System.Timers.Timer(1000);
timerThermostatDevices.Elapsed += async (sender, e) =>
{
    var tasks = thermostatDevices.Select(async device =>
    {
        var thermostatData = ShellyConnector.ShellyConnector.GetThermostatData(device);
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

ConsoleHelpers.PrintInformation("");
ConsoleHelpers.PrintInformation(" ### Done");

// Run the host to keep the application running and processing events
await host.RunAsync();
