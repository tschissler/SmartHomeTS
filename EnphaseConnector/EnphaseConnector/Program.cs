
using EnphaseConnector;
using MQTTClient;
using MQTTnet.Protocol;
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
var Leseintervall = 1000;

using (var mqttClient = new MQTTClient.MQTTClient("EnphaseConnector", "mosquitto.intern", 1883))
{
    Console.WriteLine("Connected to MQTT broker");

    while (true)
    {
        var startTime = DateTime.Now;
        if (!mqttClient.IsConnected)
        {
            await mqttClient.ConnectAsync();
        }
        await ReadDataAndSendToMQTT(tokenM1, mqttClient, "envoym1");
        await ReadDataAndSendToMQTT(tokenM3, mqttClient, "envoym3");
        Thread.Sleep(Leseintervall - (int)-DateTime.Now.Subtract(startTime).TotalMilliseconds);
    }
}

static async Task ReadDataAndSendToMQTT(EnphaseLocalToken token, MQTTClient.MQTTClient mqttClient, string deviceName)
{
    var data = await new EnphaseLib().FetchDataAsync(token, deviceName);
    if (data != null)
    {
        await mqttClient.PublishAsync($"data/electricity/{deviceName}", JsonSerializer.Serialize(data), MqttQualityOfServiceLevel.AtLeastOnce, false);

        Console.WriteLine($"{DateTime.Now} --- Data for Device {deviceName} sent via MQTT -> Battery level: {data.BatteryLevel}\t| Production: {data.PowerFromPV}");
    }
    else
    {
        Console.WriteLine("Unable to read data from Envoy");
    }
}