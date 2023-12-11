using MQTTnet;
using MQTTnet.Server;
using System;
using System.Threading.Tasks;
using MQTTnet.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        // Create a logger
        var logger = new MqttNetEventLogger();

        // Subscribe to log messages
        logger.LogMessagePublished += (s, e) =>
        {
            var traceMessage = e.LogMessage;
            Console.WriteLine($"[{traceMessage.Timestamp:O}] [{traceMessage.Level}] [{traceMessage.Source}] {traceMessage.Message}");
        };

        // Configure MQTT server options
        var mqttServerOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(1883)
            .Build();

        // Create the MQTT server with the logger
        var mqttFactory = new MqttFactory(logger);
        var mqttServer = mqttFactory.CreateMqttServer(mqttServerOptions);

        // Start the MQTT server
        await mqttServer.StartAsync();
        Console.WriteLine("MQTT Server is running.");

        // Keep the application running until user input
        Console.WriteLine("Press any key to exit...");
        Console.ReadLine();

        // Stop the MQTT server
        await mqttServer.StopAsync();
    }
}
