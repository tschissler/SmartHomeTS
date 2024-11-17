// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTClient;
using ShellyConnector;
using SmartHomeHelpers.Logging;

ConsoleHelpers.PrintInformation("ShellyConnector started");

ConsoleHelpers.PrintInformation(" ### Registering services");
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<MQTTClient.MQTTClient>(provider => new MQTTClient.MQTTClient("ShellyConnector"));
        // Register other services here
    })
    .Build();

using var serviceScope = host.Services.CreateScope();
var services = serviceScope.ServiceProvider;

var mqttClient = services.GetRequiredService<MQTTClient.MQTTClient>();

ConsoleHelpers.PrintInformation(" ### Testing connection to all devices");

var devices = ShellyDevices.GetDevices();
foreach (var device in devices)
{
    var status = ShellyConnector.ShellyConnector.GetStaus(device);
    if (status is null)
        ConsoleHelpers.PrintInformation($"  --- Could not connect to device {device.DeviceName}");
    else
        ConsoleHelpers.PrintInformation($"  - {device.DeviceName, -30} {"(" + device.IPAddress + ")", -20} {status.app + status.type, 20} => success");
}

ConsoleHelpers.PrintInformation(" ### Subscribing to topics");
await mqttClient.SubscribeToTopic("commands/shelly/#");
mqttClient.OnMessageReceived += (sender, e) =>
{
    ConsoleHelpers.PrintInformation($"  - Message received: {e.Topic}");
    var deviceName = e.Topic.Split("/")[2];
    var state = e.Payload;
    ShellyConnector.ShellyConnector.SetRelay(devices.First(i => i.DeviceName == deviceName), state);
};

ConsoleHelpers.PrintInformation(" ### Done");

// Run the host to keep the application running and processing events
await host.RunAsync();
