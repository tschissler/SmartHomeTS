
using EnphaseConnector;
using MQTTnet;
using MQTTnet.Client;
using SmartHomeHelpers.Configuration;
using System.Text.Json;


Console.WriteLine("##### Enphase Connector");

var enphaseAuth = new EnphaseLocalAuth();
string userName = Enphase.EnphaseUserName;
string password = Enphase.EnphasePassword;
string envoySerialM1 = Enphase.EnvoyM1Serial;
string envoySerialM3 = Enphase.EnvoyM3Serial;
var tokenM1 = enphaseAuth.GetTokenAsync(userName, password, envoySerialM1).Result;
Thread.Sleep(1000);
var tokenM3 = enphaseAuth.GetTokenAsync(userName, password, envoySerialM3).Result;
var Leseintervall = 3000;

var mqttFactory = new MqttFactory();

using (var mqttClient = mqttFactory.CreateMqttClient())
{
    var mqttClientOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("smarthomepi2", 32004)
        .Build();

    await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

    Console.WriteLine("Connected to MQTT broker");

    while (true)
    {
        var startTime = DateTime.Now;
        if (!mqttClient.IsConnected)
        {
            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        }
        await ReadDataAndSendToMQTT(tokenM1, mqttClient, "envoym1");
        await ReadDataAndSendToMQTT(tokenM3, mqttClient, "envoym3");
        Thread.Sleep(Leseintervall- (int)-DateTime.Now.Subtract(startTime).TotalMilliseconds);
    }

    await mqttClient.DisconnectAsync();

}

static async Task ReadDataAndSendToMQTT(EnphaseLocalToken token, IMqttClient mqttClient, string deviceName)
{
    var data = await new EnphaseLib().FetchDataAsync(token, deviceName);
    if (data != null)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
        .WithTopic($"data/electricity/{deviceName}")
        .WithPayload(JsonSerializer.Serialize(data))
        .Build();

        await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);

        Console.WriteLine($"{DateTime.Now} --- Data for Device {deviceName} sent via MQTT -> Battery level: {data.BatteryLevel}\t| Production: {data.PowerFromPV}");
    }
    else
    {
        Console.WriteLine("Unable to read data from Envoy");
    }
}